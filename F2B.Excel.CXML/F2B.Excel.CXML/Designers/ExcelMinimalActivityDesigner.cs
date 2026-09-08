using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;

namespace F2B.Excel.CXML
{
    /// <summary>
    /// Minimal designer: enables ArgumentType attached property for generic activities.
    /// </summary>
    public sealed class ExcelMinimalActivityDesigner : ActivityDesigner
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
