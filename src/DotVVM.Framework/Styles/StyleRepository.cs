﻿using DotVVM.Framework.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotVVM.Framework.Styles
{
    public class StyleRepository
    {
        public List<IStyle> Styles { get; set; } = new List<IStyle>();

        public StyleMatcher CreateMatcher()
        {
            return new StyleMatcher(Styles);
        }

        public StyleBuilder<T> Register<T>(Func<StyleMatchContext, bool> matcher = null, bool allowDerived = true)
            where T: DotvvmControl
        {
            var styleBuilder = new StyleBuilder<T>(matcher, allowDerived);
            Styles.Add(styleBuilder.GetStyle());
            return styleBuilder;
        }

        public StyleBuilder<HtmlGenericControl> Register(string tagName)
        {
            return Register<HtmlGenericControl>(m => (string)m.Control.ContructorParameters[0] == tagName, false);
        }
    }
}
