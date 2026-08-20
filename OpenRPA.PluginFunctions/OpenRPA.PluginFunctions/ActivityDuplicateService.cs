using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.Services;
using System.Activities.Presentation.View;
using System.Activities.Statements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xaml;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Ctrl+D: deep-clone the selected activity and insert it as the next sibling
    /// (same parent collection, after the original). Unlike paste, never inserts into
    /// the selected container's body.
    /// </summary>
    internal static class ActivityDuplicateService
    {
        private static readonly MethodInfo DoCopyItemsMethod = typeof(CutCopyPasteHelper).GetMethod(
            "DoCopy",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(List<ModelItem>), typeof(EditingContext) },
            null);

        private static readonly MethodInfo GetFromClipboardMethod = typeof(CutCopyPasteHelper).GetMethod(
            "GetFromClipboard",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(List<object>).MakeByRefType(), typeof(EditingContext) },
            null);

        private static readonly MethodInfo CanCopyItemMethod = typeof(CutCopyPasteHelper).GetMethod(
            "CanCopy",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(ModelItem) },
            null);

        public static bool TryDuplicateSelection()
        {
            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer?.WorkflowDesigner?.Context == null)
            {
                return false;
            }

            EditingContext context = designer.WorkflowDesigner.Context;
            Selection selection = context.Items.GetValue<Selection>();
            ModelItem primary = selection != null ? selection.PrimarySelection : null;
            if (primary == null)
            {
                primary = designer.SelectedActivity;
            }

            List<ModelItem> selected = new List<ModelItem>();
            if (selection != null && selection.SelectionCount > 0)
            {
                selected.AddRange(selection.SelectedObjects);
            }
            else if (primary != null)
            {
                selected.Add(primary);
            }

            List<ModelItem> targets = selected
                .Select(ResolveDuplicableItem)
                .Where(item => item != null)
                .Distinct()
                .ToList();

            if (targets.Count == 0)
            {
                return false;
            }

            var plans = new List<DuplicatePlan>();
            foreach (ModelItem item in targets)
            {
                ModelItemCollection collection;
                int index;
                if (!TryGetContainingCollection(item, out collection, out index))
                {
                    continue;
                }

                object clone = CloneModelItem(item, context);
                if (clone == null)
                {
                    continue;
                }

                plans.Add(new DuplicatePlan
                {
                    Collection = collection,
                    Index = index,
                    Clone = clone
                });
            }

            if (plans.Count == 0)
            {
                return false;
            }

            Exception error = null;
            ModelItem lastCreated = null;
            try
            {
                ModelService modelService = context.Services.GetService<ModelService>();
                ModelItem editRoot = modelService != null ? modelService.Root : plans[0].Collection;
                using (ModelEditingScope scope = editRoot.BeginEdit("Duplicate activity"))
                {
                    foreach (IGrouping<ModelItemCollection, DuplicatePlan> group in plans.GroupBy(p => p.Collection))
                    {
                        foreach (DuplicatePlan plan in group.OrderByDescending(p => p.Index))
                        {
                            int insertAt = plan.Index + 1;
                            if (insertAt > plan.Collection.Count)
                            {
                                insertAt = plan.Collection.Count;
                            }

                            lastCreated = plan.Collection.Insert(insertAt, plan.Clone);
                        }
                    }

                    scope.Complete();
                }

                if (lastCreated != null)
                {
                    designer.SelectedActivity = lastCreated;
                    Selection.SelectOnly(context, lastCreated);
                    try
                    {
                        lastCreated.Focus(20);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            return error == null && lastCreated != null;
        }

        private static ModelItem ResolveDuplicableItem(ModelItem item)
        {
            if (item == null)
            {
                return null;
            }

            // Selecting an activity hosted by FlowStep → duplicate the FlowStep node.
            if (typeof(Activity).IsAssignableFrom(item.ItemType)
                && item.Parent != null
                && typeof(FlowStep).IsAssignableFrom(item.Parent.ItemType)
                && item.Source != null
                && string.Equals(item.Source.Name, "Action", StringComparison.Ordinal))
            {
                item = item.Parent;
            }

            if (item.Parent == null || !IsDuplicableType(item.ItemType))
            {
                return null;
            }

            ModelItemCollection collection;
            int index;
            if (!TryGetContainingCollection(item, out collection, out index))
            {
                return null;
            }

            return item;
        }

        private static bool IsDuplicableType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (typeof(ActivityBuilder).IsAssignableFrom(type)
                || typeof(DynamicActivity).IsAssignableFrom(type))
            {
                return false;
            }

            return typeof(Activity).IsAssignableFrom(type)
                || typeof(FlowNode).IsAssignableFrom(type);
        }

        private static bool TryGetContainingCollection(
            ModelItem item,
            out ModelItemCollection collection,
            out int index)
        {
            collection = null;
            index = -1;
            if (item == null)
            {
                return false;
            }

            if (item.Source != null && item.Source.IsCollection && item.Source.Collection != null)
            {
                collection = item.Source.Collection;
                index = collection.IndexOf(item);
                if (index >= 0)
                {
                    return true;
                }
            }

            ModelItem parent = item.Parent;
            if (parent?.Properties == null)
            {
                return false;
            }

            foreach (ModelProperty property in parent.Properties)
            {
                if (property == null || !property.IsCollection || property.Collection == null)
                {
                    continue;
                }

                int found = property.Collection.IndexOf(item);
                if (found >= 0)
                {
                    collection = property.Collection;
                    index = found;
                    return true;
                }
            }

            return false;
        }

        private static object CloneModelItem(ModelItem item, EditingContext context)
        {
            object viaClipboard = CloneViaDesignerClipboard(item, context);
            if (viaClipboard != null)
            {
                return viaClipboard;
            }

            return CloneViaXaml(item.GetCurrentValue());
        }

        private static object CloneViaDesignerClipboard(ModelItem item, EditingContext context)
        {
            try
            {
                if (item == null || context == null || DoCopyItemsMethod == null || GetFromClipboardMethod == null)
                {
                    return null;
                }

                if (CanCopyItemMethod != null)
                {
                    object can = CanCopyItemMethod.Invoke(null, new object[] { item });
                    if (can is bool && !(bool)can)
                    {
                        return null;
                    }
                }

                DoCopyItemsMethod.Invoke(null, new object[] { new List<ModelItem> { item }, context });

                object[] args = new object[] { null, context };
                var data = GetFromClipboardMethod.Invoke(null, args) as List<object>;
                if (data == null || data.Count == 0 || data[0] == null)
                {
                    return null;
                }

                object clone = data[0];
                var asModel = clone as ModelItem;
                if (asModel != null)
                {
                    clone = asModel.GetCurrentValue();
                }

                return clone;
            }
            catch
            {
                return null;
            }
        }

        private static object CloneViaXaml(object value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                string xaml = XamlServices.Save(value);
                if (string.IsNullOrWhiteSpace(xaml))
                {
                    return null;
                }

                return XamlServices.Load(new StringReader(xaml));
            }
            catch
            {
                return null;
            }
        }

        private sealed class DuplicatePlan
        {
            public ModelItemCollection Collection;
            public int Index;
            public object Clone;
        }
    }
}
