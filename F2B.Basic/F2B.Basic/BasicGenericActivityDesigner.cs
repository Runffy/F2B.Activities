using System.Activities.Presentation;
using System.Activities.Presentation.Model;

namespace F2B.Basic
{
    /// <summary>
    /// Minimal designer that wires ArgumentType for generic activities (CXML ReadCell pattern).
    /// </summary>
    public sealed class BasicGenericActivityDesigner : ActivityDesigner
    {
        protected override void OnModelItemChanged(object newItem)
        {
            base.OnModelItemChanged(newItem);
            if (ModelItem != null && ModelItem.ItemType.IsGenericType)
            {
                GenericArgumentTypeUpdater.Attach(ModelItem);
            }
        }
    }
}
