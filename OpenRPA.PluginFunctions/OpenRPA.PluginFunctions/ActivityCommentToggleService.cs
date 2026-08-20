using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.Services;
using System.Activities.Presentation.View;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Ctrl+/: toggle Comment Out around selected Sequence.Activities items.
    /// </summary>
    internal static class ActivityCommentToggleService
    {
        private static Type _commentOutType;
        private static bool _commentOutTypeResolved;

        public static bool TryToggle()
        {
            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer?.WorkflowDesigner?.Context == null)
            {
                return false;
            }

            EditingContext context = designer.WorkflowDesigner.Context;
            Selection selection = context.Items.GetValue<Selection>();
            List<ModelItem> raw = CollectSelection(selection, designer);
            if (raw.Count == 0)
            {
                return false;
            }

            // Designer selection often points at nested property ModelItems; walk up to the
            // Activity that actually sits in a Sequence.Activities collection.
            List<ModelItem> members = raw
                .Select(ResolveActivitiesMember)
                .Where(item => item != null)
                .Distinct()
                .ToList();

            List<ModelItem> effective = CollapseChildSelections(members);
            if (effective.Count == 0)
            {
                MessageBox.Show(
                    "Ctrl+/ 目前仅支持 Sequence 内的 activity。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            if (effective.Count == 1 && IsCommentOut(effective[0]))
            {
                return TryUncomment(designer, context, effective[0]);
            }

            if (effective.Any(IsCommentOut))
            {
                MessageBox.Show(
                    "Ctrl+/ 仅支持对单个 Comment Out 取消注释，或对常规 activity 添加注释。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            ModelItemCollection collection;
            List<IndexedItem> indexed;
            if (!TryBuildIndexedActivities(effective, out collection, out indexed))
            {
                MessageBox.Show(
                    "Ctrl+/ 目前仅支持同一 Sequence 内的 activity。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            if (!AreContiguous(indexed))
            {
                MessageBox.Show(
                    "Ctrl+/ 快捷键对不连续的多个 activity 无效。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            return TryComment(designer, context, collection, indexed);
        }

        private static List<ModelItem> CollectSelection(Selection selection, IDesigner designer)
        {
            var list = new List<ModelItem>();
            if (selection != null)
            {
                if (selection.PrimarySelection != null)
                {
                    list.Add(selection.PrimarySelection);
                }

                if (selection.SelectionCount > 0)
                {
                    list.AddRange(selection.SelectedObjects);
                }
            }

            if (list.Count == 0 && designer.SelectedActivity != null)
            {
                list.Add(designer.SelectedActivity);
            }

            return list.Where(item => item != null).Distinct().ToList();
        }

        /// <summary>
        /// Walk up until we find a ModelItem that is a direct child of Sequence.Activities.
        /// </summary>
        private static ModelItem ResolveActivitiesMember(ModelItem start)
        {
            for (ModelItem current = start; current != null; current = current.Parent)
            {
                ModelItemCollection unusedCollection;
                int unusedIndex;
                if (TryGetActivitiesCollection(current, out unusedCollection, out unusedIndex))
                {
                    return current;
                }
            }

            return null;
        }

        /// <summary>
        /// If a parent is selected, ignore nested child selections.
        /// </summary>
        private static List<ModelItem> CollapseChildSelections(IList<ModelItem> selected)
        {
            var set = new HashSet<ModelItem>(selected);
            var result = new List<ModelItem>();
            foreach (ModelItem item in selected)
            {
                if (HasSelectedAncestor(item, set))
                {
                    continue;
                }

                result.Add(item);
            }

            return result;
        }

        private static bool HasSelectedAncestor(ModelItem item, HashSet<ModelItem> selected)
        {
            ModelItem parent = item != null ? item.Parent : null;
            while (parent != null)
            {
                if (selected.Contains(parent))
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }

        private static bool TryBuildIndexedActivities(
            IList<ModelItem> items,
            out ModelItemCollection collection,
            out List<IndexedItem> indexed)
        {
            collection = null;
            indexed = new List<IndexedItem>();

            foreach (ModelItem item in items)
            {
                ModelItemCollection col;
                int index;
                if (!TryGetActivitiesCollection(item, out col, out index))
                {
                    return false;
                }

                if (collection == null)
                {
                    collection = col;
                }
                else if (!ReferenceEquals(collection, col))
                {
                    return false;
                }

                indexed.Add(new IndexedItem { Item = item, Index = index });
            }

            indexed.Sort((a, b) => a.Index.CompareTo(b.Index));
            return indexed.Count > 0;
        }

        private static bool AreContiguous(IList<IndexedItem> indexed)
        {
            if (indexed == null || indexed.Count <= 1)
            {
                return true;
            }

            for (int i = 1; i < indexed.Count; i++)
            {
                if (indexed[i].Index != indexed[i - 1].Index + 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetActivitiesCollection(
            ModelItem item,
            out ModelItemCollection collection,
            out int index)
        {
            collection = null;
            index = -1;
            if (item?.Parent == null)
            {
                return false;
            }

            ModelItem parent = item.Parent;
            ModelProperty activitiesProperty = parent.Properties["Activities"];
            if (activitiesProperty?.Collection == null)
            {
                // Some hosts expose the collection via Source rather than Parent.Properties.
                if (item.Source != null && item.Source.IsCollection && item.Source.Collection != null
                    && string.Equals(item.Source.Name, "Activities", StringComparison.Ordinal))
                {
                    collection = item.Source.Collection;
                    index = collection.IndexOf(item);
                    return index >= 0;
                }

                return false;
            }

            collection = activitiesProperty.Collection;
            index = collection.IndexOf(item);
            return index >= 0;
        }

        private static bool TryComment(
            IDesigner designer,
            EditingContext context,
            ModelItemCollection collection,
            List<IndexedItem> indexed)
        {
            Type commentOutType = ResolveCommentOutType();
            if (commentOutType == null)
            {
                MessageBox.Show(
                    "未找到 OpenRPA.Activities.CommentOut 类型。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }

            Exception error = null;
            ModelItem created = null;
            try
            {
                ModelService modelService = context.Services.GetService<ModelService>();
                ModelItem editRoot = modelService != null ? modelService.Root : collection;
                using (ModelEditingScope scope = editRoot.BeginEdit("Comment Out"))
                {
                    int insertAt = indexed[0].Index;
                    var values = new List<Activity>(indexed.Count);
                    foreach (IndexedItem entry in indexed)
                    {
                        object current = entry.Item.GetCurrentValue();
                        var activity = current as Activity;
                        if (activity == null)
                        {
                            throw new InvalidOperationException(
                                "Selected item is not an Activity: " + entry.Item.ItemType);
                        }

                        values.Add(activity);
                    }

                    for (int i = indexed.Count - 1; i >= 0; i--)
                    {
                        collection.Remove(indexed[i].Item);
                    }

                    Activity body;
                    if (values.Count == 1)
                    {
                        body = values[0];
                    }
                    else
                    {
                        var sequence = new Sequence();
                        foreach (Activity activity in values)
                        {
                            sequence.Activities.Add(activity);
                        }

                        body = sequence;
                    }

                    object commentInstance = Activator.CreateInstance(commentOutType);
                    created = collection.Insert(insertAt, commentInstance);
                    created.Properties["Body"].SetValue(body);
                    scope.Complete();
                }

                if (created != null)
                {
                    designer.SelectedActivity = created;
                    Selection.SelectOnly(context, created);
                    try
                    {
                        created.Focus(20);
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

            if (error != null)
            {
                MessageBox.Show(
                    "Comment Out 失败: " + error.Message,
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }

            return created != null;
        }

        private static bool TryUncomment(IDesigner designer, EditingContext context, ModelItem comment)
        {
            ModelItemCollection collection;
            int index;
            if (!TryGetActivitiesCollection(comment, out collection, out index))
            {
                MessageBox.Show(
                    "Ctrl+/ 取消注释目前仅支持位于 Sequence.Activities 中的 Comment Out。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            ModelProperty bodyProperty = comment.Properties["Body"];
            if (bodyProperty?.Value == null)
            {
                MessageBox.Show(
                    "Comment Out 的 Body 为空，无法取消注释。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            object bodyValue = bodyProperty.Value.GetCurrentValue();
            if (bodyValue == null)
            {
                MessageBox.Show(
                    "Comment Out 的 Body 为空，无法取消注释。",
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }

            Exception error = null;
            ModelItem restored = null;
            try
            {
                ModelService modelService = context.Services.GetService<ModelService>();
                ModelItem editRoot = modelService != null ? modelService.Root : collection;
                using (ModelEditingScope scope = editRoot.BeginEdit("Uncomment"))
                {
                    // Detach body before removing the shell so the tree keeps the instance.
                    comment.Properties["Body"].SetValue(null);
                    collection.Remove(comment);
                    if (index > collection.Count)
                    {
                        index = collection.Count;
                    }

                    restored = collection.Insert(index, bodyValue);
                    scope.Complete();
                }

                if (restored != null)
                {
                    designer.SelectedActivity = restored;
                    Selection.SelectOnly(context, restored);
                    try
                    {
                        restored.Focus(20);
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

            if (error != null)
            {
                MessageBox.Show(
                    "取消注释失败: " + error.Message,
                    "Comment Out",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return true;
            }

            return restored != null;
        }

        private static bool IsCommentOut(ModelItem item)
        {
            if (item?.ItemType == null)
            {
                return false;
            }

            Type type = item.ItemType;
            return string.Equals(type.Name, "CommentOut", StringComparison.Ordinal)
                && string.Equals(type.Namespace, "OpenRPA.Activities", StringComparison.Ordinal);
        }

        private static Type ResolveCommentOutType()
        {
            if (_commentOutTypeResolved)
            {
                return _commentOutType;
            }

            _commentOutTypeResolved = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType("OpenRPA.Activities.CommentOut", false);
                    if (type != null)
                    {
                        _commentOutType = type;
                        break;
                    }
                }
                catch
                {
                }
            }

            if (_commentOutType == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetExportedTypes();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        if (string.Equals(type.FullName, "OpenRPA.Activities.CommentOut", StringComparison.Ordinal))
                        {
                            _commentOutType = type;
                            break;
                        }
                    }

                    if (_commentOutType != null)
                    {
                        break;
                    }
                }
            }

            return _commentOutType;
        }

        private sealed class IndexedItem
        {
            public ModelItem Item;
            public int Index;
        }
    }
}
