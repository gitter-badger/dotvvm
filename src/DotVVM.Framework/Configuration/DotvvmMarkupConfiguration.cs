using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using DotVVM.Framework.Controls;

namespace DotVVM.Framework.Configuration
{
    public class DotvvmMarkupConfiguration
    {

        /// <summary>
        /// Gets the registered control namespaces.
        /// </summary>
        [JsonProperty("controls", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<DotvvmControlConfiguration> Controls { get; private set; }

        /// <summary>
        /// Gets or sets the list of referenced assemblies.
        /// </summary>
        [JsonProperty("assemblies", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> Assemblies { get; private set; }

        /// <summary>
        /// Gets a list of HTML attribute transforms.
        /// </summary>
        [JsonProperty("htmlAttributeTransforms")]
        public Dictionary<HtmlTagAttributePair, HtmlAttributeTransformConfiguration> HtmlAttributeTransforms { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotvvmMarkupConfiguration"/> class.
        /// </summary>
        public DotvvmMarkupConfiguration()
        {
            Controls = new List<DotvvmControlConfiguration>();
            Assemblies = new List<string>();
            HtmlAttributeTransforms = new Dictionary<HtmlTagAttributePair, HtmlAttributeTransformConfiguration>()
            {
                {
                    new HtmlTagAttributePair { TagName = "a", AttributeName = "href" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "link", AttributeName = "href" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "img", AttributeName = "src" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "iframe", AttributeName = "src" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
                {
                    new HtmlTagAttributePair { TagName = "script", AttributeName = "src" },
                    new HtmlAttributeTransformConfiguration() { Type = typeof(TranslateVirtualPathHtmlAttributeTransformer) }
                },
            };
        }


        /// <summary>
        /// Adds the assembly to the list of required assemblies.
        /// </summary>
        public void AddAssembly(string assemblyName)
        {
            if (!Assemblies.Contains(assemblyName))
            {
                Assemblies.Add(assemblyName);
            }
        }
    }
}