﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Parser.Dothtml.Parser;
using DotVVM.Framework.Runtime.Compilation.ResolvedControlTree;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls.Infrastructure;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Parser;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Utils;
using System.Collections;
using System.Linq.Expressions;
using DotVVM.Framework.Exceptions;
using System.Net;

namespace DotVVM.Framework.Runtime.Compilation
{
    public class ControlTreeResolver : IControlTreeResolver
    {
        private IControlResolver controlResolver;
        private IBindingParser bindingParser;

        public ControlTreeResolver(DotvvmConfiguration configuration)
        {
            controlResolver = configuration.ServiceLocator.GetService<IControlResolver>();
            bindingParser = configuration.ServiceLocator.GetService<IBindingParser>();
        }

        public ResolvedTreeRoot ResolveTree(DothtmlRootNode root, string fileName)
        {
            var wrapperType = ResolveWrapperType(root, fileName.EndsWith(".dotcontrol", StringComparison.Ordinal) ? typeof(DotvvmMarkupControl) : typeof(DotvvmView));

            // We need to call BuildControlMetadata instead of ResolveControl. The control builder for the control doesn't have to be compiled yet so the 
            // metadata would be incomplete and ResolveControl caches them internally. BuildControlMetadata just builds the metadata and the control is
            // actually resolved when the control builder is ready and the metadata are complete.
            var viewMetadata = controlResolver.BuildControlMetadata(new ControlType(wrapperType, virtualPath: fileName));
            var view = new ResolvedTreeRoot(viewMetadata, root, null);

            foreach (var directive in root.Directives)
            {
                if (!string.Equals(directive.Name, Constants.BaseTypeDirective, StringComparison.InvariantCultureIgnoreCase))
                {
                    view.Directives.Add(directive.Name, directive.Value);
                }
            }

            ResolveViewModel(fileName, view, wrapperType);

            foreach (var node in root.Content)
            {
                view.Content.Add(ProcessNode(node, viewMetadata, view.DataContextTypeStack));
            }
            return view;
        }

        private void ResolveViewModel(string fileName, ResolvedTreeRoot view, Type wrapperType)
        {
            if (!view.Directives.ContainsKey(Constants.ViewModelDirectiveName))
            {
                throw new DotvvmCompilationException($"The @viewModel directive is missing in the page '{fileName}'!", view.DothtmlNode.Tokens.Take(1));
            }

            var viewModelTypeName = view.Directives[Constants.ViewModelDirectiveName];
            var viewModelType = ReflectionUtils.FindType(viewModelTypeName);
            if (viewModelType == null)
            {
                throw new DotvvmCompilationException($"The type '{viewModelTypeName}' required in the @viewModel directive in was not found!",
                    (view.DothtmlNode as DothtmlRootNode)?.Directives?.FirstOrDefault(d => string.Equals(d.Name, Constants.ViewModelDirectiveName, StringComparison.InvariantCultureIgnoreCase))?.Tokens);
            }
            view.DataContextTypeStack = new DataContextStack(viewModelType)
            {
                RootControlType = wrapperType
            };
        }

        private ResolvedControl ProcessNode(DothtmlNode node, ControlResolverMetadata parentMetadata, DataContextStack dataContext)
        {
            try
            {
                if (node is DothtmlBindingNode)
                {
                    EnsureContentAllowed(parentMetadata);

                    // binding in text
                    var binding = (DothtmlBindingNode)node;
                    var literal = new ResolvedControl(controlResolver.ResolveControl(typeof(Literal)), node, dataContext);
                    literal.SetProperty(new ResolvedPropertyBinding(Literal.TextProperty, ProcessBinding(binding, dataContext)));
                    literal.SetProperty(new ResolvedPropertyValue(Literal.RenderSpanElementProperty, false));
                    return literal;
                }
                else if(node is DotHtmlCommentNode)
                {
                    var commentNode = node as DotHtmlCommentNode;

                    string text = commentNode.IsServerSide ?  "" : "<!--" + commentNode.Value + "-->";
                    var literal = new ResolvedControl(controlResolver.ResolveControl(typeof(RawLiteral)), node, dataContext);
                    literal.ContructorParameters = new object[] { text, commentNode.Value, true };
                    return literal;
                }
                else if (node is DothtmlLiteralNode)
                {
                    var literalNode = ((DothtmlLiteralNode)node);
                    // text content
                    var whitespace = string.IsNullOrWhiteSpace(literalNode.Value);
                    if (!whitespace) EnsureContentAllowed(parentMetadata);

                    string text;
                    
                    if (literalNode.Escape)
                        text = WebUtility.HtmlEncode(literalNode.Value);
                    else text = literalNode.Value;

                    var literal = new ResolvedControl(controlResolver.ResolveControl(typeof(RawLiteral)), node, dataContext);
                    literal.ContructorParameters = new object[] { text, literalNode.Value, whitespace };
                    return literal;
                }
                else if (node is DothtmlElementNode)
                {
                    // HTML element
                    var element = (DothtmlElementNode)node;
                    EnsureContentAllowed(parentMetadata);

                    // the element is the content
                    return ProcessObjectElement(element, dataContext);
                }
                else
                {
                    throw new NotSupportedException($"The node of type '{node.GetType()}' is not supported!");
                }
            }
            catch (DotvvmCompilationException ex)
            {
                if (ex.Tokens == null)
                {
                    ex.Tokens = node.Tokens;
                    ex.ColumnNumber = node.Tokens.First().ColumnNumber;
                    ex.LineNumber = node.Tokens.First().LineNumber;
                }
                throw;
            }
            catch (Exception ex)
            {
                throw new DotvvmCompilationException("", ex, node.Tokens);
            }
        }

        /// <summary>
        /// Processes the HTML element that represents a new object.
        /// </summary>
        private ResolvedControl ProcessObjectElement(DothtmlElementNode element, DataContextStack dataContext)
        {
            object[] constructorParameters;

            var controlMetadata = controlResolver.ResolveControl(element.TagPrefix, element.TagName, out constructorParameters);
            var control = new ResolvedControl(controlMetadata, element, dataContext)
            {
                ContructorParameters = constructorParameters
            };

            var dataContextAttribute = element.Attributes.FirstOrDefault(a => a.AttributeName == "DataContext");
            if (dataContextAttribute != null)
            {
                ProcessAttribute(dataContextAttribute, control, dataContext);
            }
            if (control.Properties.ContainsKey(DotvvmBindableObject.DataContextProperty) && control.Properties[DotvvmBindableObject.DataContextProperty] is ResolvedPropertyBinding)
            {
                dataContext = new DataContextStack(
                    ((ResolvedPropertyBinding)control.Properties[DotvvmBindableObject.DataContextProperty]).Binding.GetExpression().Type,
                    dataContext);
                control.DataContextTypeStack = dataContext;
            }
            if (controlMetadata.DataContextConstraint != null && !controlMetadata.DataContextConstraint.IsAssignableFrom(dataContext.DataContextType))
            {
                throw new DotvvmCompilationException($"The control '{controlMetadata.Name}' requires a DataContext of type '{controlMetadata.DataContextConstraint.FullName}'!", element.Tokens);
            }

            // set properties from attributes
            foreach (var attribute in element.Attributes.Where(a => a.AttributeName != "DataContext"))
            {
                ProcessAttribute(attribute, control, dataContext);
            }

            var typeChange = DataContextChangeAttribute.GetDataContextExpression(dataContext, control);
            if (typeChange != null)
            {
                dataContext = new DataContextStack(typeChange, dataContext);
            }

            ProcessControlContent(element.Content, control);

            // check required properties
            var missingProperties = control.Metadata.Properties.Values.Where(p => p.MarkupOptions.Required && !control.Properties.ContainsKey(p));
            if (missingProperties.Any())
            {
                throw new DotvvmCompilationException($"The control '{ control.Metadata.Name }' is missing required properties: { string.Join(", ", missingProperties.Select(p => "'" + p.Name + "'")) }.", control.DothtmlNode.Tokens);
            }
            return control;
        }

        private ResolvedBinding ProcessBinding(DothtmlBindingNode node, DataContextStack context)
        {
            var value = node.Value;
            var bindingOptions = controlResolver.ResolveBinding(node.Name);
            Expression expression = null;
            Exception parsingError = null;
            try
            {
                expression = bindingParser.Parse(value, context, bindingOptions);
            }
            catch (Exception exception)
            {
                parsingError = exception;
            }
            return new ResolvedBinding()
            {
                BindingType = bindingOptions.BindingType,
                Value = node.Value,
                Expression = expression,
                DataContextTypeStack = context,
                ParsingError = parsingError,
                BindingNode = node
            };
        }


        /// <summary>
        /// Processes the HTML attribute.
        /// </summary>
        private void ProcessAttribute(DothtmlAttributeNode attribute, ResolvedControl control, DataContextStack dataContext)
        {
            if (attribute.AttributePrefix == "html")
            {
                if (!control.Metadata.HasHtmlAttributesCollection) throw new DotvvmCompilationException($"control { control.Metadata.Name } does not have html attribute collection", attribute.Tokens);
                control.SetHtmlAttribute(attribute.AttributeName, ProcessAttributeValue(attribute.ValueNode, dataContext));
                return;
            }

            if (!string.IsNullOrEmpty(attribute.AttributePrefix))
            {
                throw new DotvvmCompilationException("Attributes with XML namespaces are not supported!", attribute.Tokens);
            }

            // find the property
            var property = FindProperty(control.Metadata, attribute.AttributeName);
            if (property != null)
            {
                if (property.IsBindingProperty)
                {
                    var typeChange = DataContextChangeAttribute.GetDataContextExpression(dataContext, control, property);
                    if (typeChange != null)
                    {
                        dataContext = new DataContextStack(typeChange, dataContext);
                    }
                }

                if (!property.MarkupOptions.MappingMode.HasFlag(MappingMode.Attribute)) throw new DotvvmCompilationException($"The property '{ property.FullName }' cannot be used as attribute", attribute.Tokens);

                // set the property
                if (attribute.ValueNode == null)
                {
                    throw new DotvvmCompilationException($"The attribute '{property.Name}' on the control '{control.Metadata.Name}' must have a value!", attribute.Tokens);
                }
                else if (attribute.ValueNode is DothtmlValueBindingNode)
                {
                    // binding
                    var bindingNode = (attribute.ValueNode as DothtmlValueBindingNode).BindingNode;
                    if (!property.MarkupOptions.AllowBinding) throw new DotvvmCompilationException($"The property '{ property.FullName }' cannot contain binding.", bindingNode.Tokens);
                    var resolvedBinding = ProcessBinding(bindingNode, dataContext);
                    control.SetProperty(new ResolvedPropertyBinding(property, resolvedBinding));
                }
                else
                {
                    // hard-coded value in markup
                    if (!property.MarkupOptions.AllowHardCodedValue) throw new DotvvmCompilationException($"The property '{ property.FullName }' cannot contain hard coded value.", attribute.ValueNode.Tokens);

                    var textValue = attribute.ValueNode as DothtmlValueTextNode;

                    var value = ReflectionUtils.ConvertValue(textValue.Text, property.PropertyType);
                    control.SetPropertyValue(property, value);
                }
            }
            else if (control.Metadata.HasHtmlAttributesCollection)
            {
                // if the property is not found, add it as an HTML attribute
                control.SetHtmlAttribute(attribute.AttributeName, ProcessAttributeValue(attribute.ValueNode, dataContext));
            }
            else
            {
                throw new DotvvmCompilationException($"The control '{control.Metadata.Type}' does not have a property '{attribute.AttributeName}' and does not allow HTML attributes!");
            }
        }

        private object ProcessAttributeValue(DothtmlValueNode valueNode, DataContextStack dataContext)
        {
            return (valueNode is DothtmlValueBindingNode)
                    ? (object)ProcessBinding((valueNode as DothtmlValueBindingNode).BindingNode, dataContext)
                    : (valueNode as DothtmlValueTextNode)?.Text;
        }


        private void ProcessControlContent(IEnumerable<DothtmlNode> nodes, ResolvedControl control)
        {
            var content = new List<DothtmlNode>();
            bool properties = true;
            foreach (var node in nodes)
            {
                var element = node as DothtmlElementNode;
                if (element != null && properties)
                {
                    var property = FindProperty(control.Metadata, element.TagName);
                    if (property != null && string.IsNullOrEmpty(element.TagPrefix) && property.MarkupOptions.MappingMode.HasFlag(MappingMode.InnerElement))
                    {
                        content.Clear();
                        control.SetProperty(ProcessElementProperty(control, property, element.Content));
                    }
                    else
                    {
                        content.Add(node);
                        if (node.IsNotEmpty())
                        {
                            properties = false;
                        }
                    }
                }
                else
                    content.Add(node);
            }
            if (content.Any(DothtmlNodeHelper.IsNotEmpty))
            {
                if (control.Metadata.DefaultContentProperty != null)
                {
                    control.SetProperty(ProcessElementProperty(control, control.Metadata.DefaultContentProperty, content));
                }
                else
                {
                    foreach (var node in content)
                    {
                        // TODO: data context from parent
                        control.Content.Add(ProcessNode(node, control.Metadata, control.DataContextTypeStack));
                    }
                }
            }
        }

        /// <summary>
        /// Processes the element which contains property value.
        /// </summary>
        private ResolvedPropertySetter ProcessElementProperty(ResolvedControl control, DotvvmProperty property, IEnumerable<DothtmlNode> elementContent)
        {
            var dataContext = control.DataContextTypeStack;
            var typeChange = DataContextChangeAttribute.GetDataContextExpression(dataContext, control, property);
            if (typeChange != null)
            {
                dataContext = new DataContextStack(typeChange, dataContext);
            }
            // the element is a property 
            if (IsTemplateProperty(property))
            {
                // template
                return new ResolvedPropertyTemplate(property, ProcessTemplate(elementContent, dataContext));
            }
            else if (IsCollectionProperty(property))
            {
                // collection of elements
                var collection = FilterNodes<DothtmlElementNode>(elementContent, property)
                    .Select(childObject => ProcessObjectElement(childObject, dataContext));
                return new ResolvedPropertyControlCollection(property, collection.ToList());
            }
            else if (property.PropertyType == typeof(string))
            {
                // string property
                var strings = FilterNodes<DothtmlLiteralNode>(elementContent, property);
                var value = string.Concat(strings.Select(s => s.Value));
                return new ResolvedPropertyValue(property, value);
            }
            else if (IsControlProperty(property))
            {
                // new object
                var children = FilterNodes<DothtmlElementNode>(elementContent, property).ToList();
                if (children.Count > 1)
                {
                    throw new DotvvmCompilationException($"The property '{property.MarkupOptions.Name}' can have only one child element!");
                }
                else if (children.Count == 1)
                {
                    return new ResolvedPropertyControl(property, ProcessObjectElement(children[0], dataContext));
                }
                else
                {
                    return new ResolvedPropertyControl(property, null);
                }
            }
            else
            {
                throw new DotvvmCompilationException($"The property '{property.FullName}' is not supported!");
            }
        }

        private List<ResolvedControl> ProcessTemplate(IEnumerable<DothtmlNode> elementContent, DataContextStack dataContext)
        {
            var placeholderMetadata = controlResolver.ResolveControl(typeof(PlaceHolder));
            var content = elementContent.Select(e => ProcessNode(e, placeholderMetadata, dataContext));
            return content.ToList();
        }

        /// <summary>
        /// Gets the inner property elements and makes sure that no other content is present.
        /// </summary>
        private IEnumerable<TNode> FilterNodes<TNode>(IEnumerable<DothtmlNode> nodes, DotvvmProperty property) where TNode : DothtmlNode
        {
            foreach (var child in nodes)
            {
                if (child is TNode)
                {
                    yield return (TNode)child;
                }
                else if (child.IsNotEmpty())
                {
                    throw new DotvvmCompilationException($"Content is not allowed inside the property '{property.Name}'! (Conflicting node: Node {child.GetType().Name})");
                }
            }
        }


        /// <summary>
        /// Resolves the type of the wrapper.
        /// </summary>
        private Type ResolveWrapperType(DothtmlRootNode node, Type defaultType)
        {
            var wrapperType = defaultType;

            var baseControlDirective = node.Directives.SingleOrDefault(d => string.Equals(d.Name, Constants.BaseTypeDirective, StringComparison.InvariantCultureIgnoreCase));
            if (baseControlDirective != null)
            {
                wrapperType = ReflectionUtils.FindType(baseControlDirective.Value);
                if (wrapperType == null)
                {
                    throw new DotvvmCompilationException($"The type '{baseControlDirective.Value}' specified in baseType directive was not found!");
                }
                if (!typeof(DotvvmMarkupControl).IsAssignableFrom(wrapperType))
                {
                    throw new DotvvmCompilationException("Markup controls must derive from DotvvmMarkupControl class!");
                }
            }

            return wrapperType;
        }

        private DotvvmProperty FindProperty(ControlResolverMetadata parentMetadata, string name)
        {
            return parentMetadata.FindProperty(name) ?? (name.Contains(".") ? DotvvmProperty.ResolveProperty(name, false) : null);
        }

        private static void EnsureContentAllowed(ControlResolverMetadata controlMetadata)
        {
            if (!controlMetadata.IsContentAllowed)
            {
                throw new DotvvmCompilationException($"The content is not allowed inside the control '{controlMetadata.Name}'!");
            }
        }

        private static bool IsCollectionProperty(DotvvmProperty parentProperty)
        {
            return parentProperty.PropertyType.GetInterfaces().Contains(typeof(ICollection));
        }

        private static bool IsTemplateProperty(DotvvmProperty parentProperty)
        {
            return parentProperty.PropertyType == typeof(ITemplate);
        }

        private static bool IsControlProperty(DotvvmProperty property)
        {
            return typeof(DotvvmControl).IsAssignableFrom(property.PropertyType);
        }
    }
}
