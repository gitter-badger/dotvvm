﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Exceptions;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// Renders a text into the page.
    /// </summary>
    [ControlMarkupOptions(DefaultContentProperty = nameof(Html))]
    public class HtmlLiteral : HtmlGenericControl
    {

        /// <summary>
        /// Gets or sets the HTML that will be rendered in the control.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false)]
        public string Html
        {
            get { return (string)GetValue(HtmlProperty); }
            set { SetValue(HtmlProperty, value); }
        }
        public static readonly DotvvmProperty HtmlProperty =
            DotvvmProperty.Register<string, HtmlLiteral>(t => t.Html, "");


        /// <summary>
        /// Gets or sets whether the control should render a wrapper element.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public string WrapperTagName
        {
            get { return (string)GetValue(WrapperTagNameProperty); }
            set { SetValue(WrapperTagNameProperty, value); }
        }
        public static readonly DotvvmProperty WrapperTagNameProperty
            = DotvvmProperty.Register<string, HtmlLiteral>(c => c.WrapperTagName, "div");


        /// <summary>
        /// Gets or sets the name of the tag that wraps the HtmlLiteral.
        /// </summary>
        [MarkupOptions(AllowBinding = false)]
        public bool RenderWrapperTag
        {
            get { return (bool)GetValue(RenderWrapperTagProperty); }
            set { SetValue(RenderWrapperTagProperty, value); }
        }
        public static readonly DotvvmProperty RenderWrapperTagProperty
            = DotvvmProperty.Register<bool, HtmlLiteral>(c => c.RenderWrapperTag, true);



        protected override bool RendersHtmlTag => RenderWrapperTag;


        public HtmlLiteral() : base("div")
        {
        }


        protected override void AddAttributesToRender(IHtmlWriter writer, RenderContext context)
        {
            TagName = WrapperTagName;

            base.AddAttributesToRender(writer, context);

            if (!RenderWrapperTag && !RenderOnServer && HasBinding(HtmlProperty))
            {
                throw new DotvvmControlException(this, "The HtmlLiteral control doesn't support client-side rendering without wrapper tag. Enable server rendering or the wrapper tag.");
            }

            if (RenderWrapperTag && HasBinding(HtmlProperty))
            {
                writer.AddKnockoutDataBind("html", this, HtmlProperty);
            }
        }

        protected override void RenderBeginTag(IHtmlWriter writer, RenderContext context)
        {
            if (RenderWrapperTag)
            {
                base.RenderBeginTag(writer, context);
            }
            else if (HasBinding(HtmlProperty))
            {
                writer.WriteKnockoutDataBindComment("html", this, HtmlProperty);
            }
        }

        protected override void RenderContents(IHtmlWriter writer, RenderContext context)
        {
            if (RenderOnServer || !HasBinding(HtmlProperty))
            {
                writer.WriteUnencodedText(Html);       
            }
        }

        protected override void RenderEndTag(IHtmlWriter writer, RenderContext context)
        {
            if (RenderWrapperTag)
            {
                base.RenderEndTag(writer, context);
            }
            else if (HasBinding(HtmlProperty))
            {
                writer.WriteKnockoutDataBindEndComment();
            }
        }
    }
}
