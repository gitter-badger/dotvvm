﻿using DotVVM.Framework.Binding;
using DotVVM.Framework.Runtime;
using System;
using System.Collections.Generic;

namespace DotVVM.Framework.Controls
{
    public interface IDotvvmControl
    {
        DotvvmControlCollection Children { get; }
        ClientIDMode ClientIDMode { get; set; }
        string ID { get; set; }
        DotvvmControl Parent { get; set; }
        List<string> ResourceDependencies { get; }

        void EnsureControlHasId(bool autoGenerate = true);

        DotvvmControl FindControl(string id, bool throwIfNotFound = false);

        T FindControl<T>(string id, bool throwIfNotFound = false) where T : DotvvmControl;

        IEnumerable<DotvvmControl> GetAllAncestors();

        IEnumerable<DotvvmControl> GetAllDescendants(Func<DotvvmControl, bool> enumerateChildrenCondition = null);

        IEnumerable<DotvvmControl> GetChildren();

        DotvvmControl GetNamingContainer();

        DotvvmControl GetRoot();

        IEnumerable<DotvvmControl> GetThisAndAllDescendants(Func<DotvvmControl, bool> enumerateChildrenCondition = null);

        object GetValue(DotvvmProperty property, bool inherit = true);

        bool HasOnlyWhiteSpaceContent();

        bool IsPropertySet(DotvvmProperty property, bool inherit = true);

        void Render(IHtmlWriter writer, RenderContext context);

        void SetValue(DotvvmProperty property, object value);
    }
}