﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotVVM.Framework.Runtime.Compilation.ResolvedControlTree
{
    public abstract class ResolvedControlTreeVisitor : IResolvedControlTreeVisitor
    {
        public virtual void VisitControl(ResolvedControl control)
        {
            DefaultVisit(control);
        }

        public virtual void VisitPropertyBinding(ResolvedPropertyBinding propertyBinding)
        {
            DefaultVisit(propertyBinding);
        }

        public virtual void VisitPropertyValue(ResolvedPropertyValue propertyValue)
        {
            DefaultVisit(propertyValue);
        }

        public virtual void VisitView(ResolvedTreeRoot view)
        {
            DefaultVisit(view);
        }

        public virtual void VisitPropertyTemplate(ResolvedPropertyTemplate propertyTemplate)
        {
            DefaultVisit(propertyTemplate);
        }

        public virtual void VisitPropertyControlCollection(ResolvedPropertyControlCollection propertyControlCollection)
        {
            DefaultVisit(propertyControlCollection);
        }

        public virtual void VisitPropertyControl(ResolvedPropertyControl propertyControl)
        {
            DefaultVisit(propertyControl);
        }


        public virtual void DefaultVisit(IResolvedTreeNode node)
        {
            node.AcceptChildren(this);
        }
    }
}
