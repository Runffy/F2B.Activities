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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Creates activities and inserts them. Ctrl+P captures the Sequence spacer (click-between) before the popup steals focus.
    /// </summary>
    internal static class ActivityInsertService
    {
        private static InsertAnchor _paletteAnchor;

        public static void CapturePaletteInsertAnchor()
        {
            _paletteAnchor = TryReadInsertAnchor();
        }

        public static void ClearPaletteInsertAnchor()
        {
            _paletteAnchor = null;
        }

        public static void CapturePaletteInsertAnchorFrom(DependencyObject source)
        {
            InsertAnchor fromSource = TryReadInsertAnchorFrom(source);
            if (fromSource != null && fromSource.Index >= 0)
            {
                _paletteAnchor = fromSource;
                return;
            }

            _paletteAnchor = TryReadInsertAnchor();
        }

        public static bool IsSequenceSpacer(DependencyObject source)
        {
            try
            {
                if (source == null)
                {
                    return false;
                }

                WorkflowItemsPresenter presenter = FindAncestor<WorkflowItemsPresenter>(source);
                if (presenter == null)
                {
                    return false;
                }

                ItemsControl panel = GetPresenterPanel(presenter);
                int viewIndex = IndexInItemsControl(panel, source);
                return IsSpacerViewIndex(viewIndex);
            }
            catch
            {
                return false;
            }
        }
        public static Activity CreateActivity(Type type)
        {
            if (type == null || type.IsAbstract)
            {
                return null;
            }

            type = CloseOpenGenericIfNeeded(type);
            if (type == null || type.IsAbstract || type.ContainsGenericParameters)
            {
                return null;
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }

            if (instance is IActivityTemplateFactory factory)
            {
                try
                {
                    return factory.Create(null);
                }
                catch
                {
                    return null;
                }
            }

            return instance as Activity;
        }

        /// <summary>
        /// Toolbox shows open generics (AddToCollection&lt;&gt;, ForEachOf&lt;&gt;). Close them using
        /// DefaultTypeArgumentAttribute when present, otherwise string for each type parameter.
        /// </summary>
        private static Type CloseOpenGenericIfNeeded(Type type)
        {
            if (type == null || !type.IsGenericTypeDefinition)
            {
                return type;
            }

            Type[] parameters = type.GetGenericArguments();
            if (parameters == null || parameters.Length == 0)
            {
                return type;
            }

            var typeArgs = new Type[parameters.Length];
            Type defaultArg = TryGetDefaultTypeArgument(type);
            for (int i = 0; i < parameters.Length; i++)
            {
                typeArgs[i] = defaultArg ?? typeof(string);
            }

            try
            {
                return type.MakeGenericType(typeArgs);
            }
            catch
            {
                return null;
            }
        }

        private static Type TryGetDefaultTypeArgument(Type type)
        {
            try
            {
                object[] attrs = type.GetCustomAttributes(typeof(DefaultTypeArgumentAttribute), true);
                if (attrs != null && attrs.Length > 0)
                {
                    var attr = attrs[0] as DefaultTypeArgumentAttribute;
                    if (attr != null && attr.Type != null)
                    {
                        return attr.Type;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static bool TryAddActivity(Type type)
        {
            Activity activity = CreateActivity(type);
            if (activity == null)
            {
                return false;
            }

            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer == null)
            {
                return false;
            }

            Exception error = null;
            GenericTools.RunUI(() =>
            {
                try
                {
                    InsertAnchor anchor = _paletteAnchor ?? TryReadInsertAnchor();
                    _paletteAnchor = null;
                    if (anchor != null && anchor.Collection != null && anchor.Index >= 0)
                    {
                        InsertAtAnchor(designer, activity, anchor);
                    }
                    else
                    {
                        designer.AddActivity(activity);
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }, 10000);

            return error == null;
        }

        public static bool TryFocusModelItem(IDesigner designer, ModelItem item)
        {
            if (designer == null || item == null)
            {
                return false;
            }

            try
            {
                Selection.SelectOnly(designer.WorkflowDesigner.Context, item);
                item.Focus(20);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<ModelItem> GetAllActivities(IDesigner designer)
        {
            var list = new List<ModelItem>();
            if (designer == null || designer.WorkflowDesigner == null)
            {
                return list;
            }

            try
            {
                ModelService modelService = designer.WorkflowDesigner.Context.Services.GetService<ModelService>();
                if (modelService == null)
                {
                    return list;
                }

                list.AddRange(modelService.Find(modelService.Root, typeof(Activity)));
            }
            catch
            {
            }

            return list;
        }

        public static string GetDisplayName(ModelItem item)
        {
            if (item == null)
            {
                return null;
            }

            try
            {
                ModelProperty prop = item.Properties["DisplayName"];
                if (prop != null && prop.ComputedValue != null)
                {
                    string text = Convert.ToString(prop.ComputedValue);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }

                object current = item.GetCurrentValue();
                Activity activity = current as Activity;
                if (activity != null && !string.IsNullOrWhiteSpace(activity.DisplayName))
                {
                    return activity.DisplayName.Trim();
                }

                return item.ItemType != null ? item.ItemType.Name : null;
            }
            catch
            {
                return null;
            }
        }

        private static void InsertAtAnchor(IDesigner designer, Activity activity, InsertAnchor anchor)
        {
            if (designer == null || designer.WorkflowDesigner == null || activity == null || anchor == null || anchor.Collection == null)
            {
                return;
            }

            ModelService modelService = designer.WorkflowDesigner.Context.Services.GetService<ModelService>();
            if (modelService == null)
            {
                designer.AddActivity(activity);
                return;
            }

            ModelItem newItem = null;
            using (ModelEditingScope scope = modelService.Root.BeginEdit("Implementation"))
            {
                if (string.IsNullOrEmpty(activity.DisplayName))
                {
                    activity.DisplayName = "Activity";
                }

                int index = anchor.Index;
                if (index > anchor.Collection.Count)
                {
                    index = anchor.Collection.Count;
                }

                if (IsNodesCollection(anchor.Collection))
                {
                    newItem = anchor.Collection.Insert(index, new FlowStep { Action = activity });
                }
                else
                {
                    newItem = anchor.Collection.Insert(index, activity);
                }

                scope.Complete();
            }

            if (newItem != null)
            {
                try
                {
                    Selection.SelectOnly(designer.WorkflowDesigner.Context, newItem);
                    newItem.Focus(20);
                }
                catch
                {
                }
            }
        }

        private static bool IsNodesCollection(ModelItemCollection collection)
        {
            try
            {
                ModelItem parent = collection.Parent;
                if (parent == null || parent.Properties["Nodes"] == null)
                {
                    return false;
                }

                return ReferenceEquals(parent.Properties["Nodes"].Collection, collection);
            }
            catch
            {
                return false;
            }
        }

        private static InsertAnchor TryReadInsertAnchor()
        {
            try
            {
                DependencyObject start = Keyboard.FocusedElement as DependencyObject;
                InsertAnchor fromFocus = TryReadInsertAnchorFrom(start);
                if (fromFocus != null && fromFocus.Index >= 0)
                {
                    return fromFocus;
                }

                DependencyObject over = Mouse.DirectlyOver as DependencyObject;
                InsertAnchor fromMouse = TryReadInsertAnchorFrom(over);
                if (fromMouse != null && fromMouse.Index >= 0)
                {
                    return fromMouse;
                }

                return fromFocus ?? fromMouse;
            }
            catch
            {
                return null;
            }
        }

        private static InsertAnchor TryReadInsertAnchorFrom(DependencyObject start)
        {
            if (start == null)
            {
                return null;
            }

            WorkflowItemsPresenter presenter = FindAncestor<WorkflowItemsPresenter>(start);
            if (presenter == null || presenter.Items == null)
            {
                return null;
            }

            int index = ReadSelectedSpacerIndex(presenter);
            if (index < 0)
            {
                index = ReadSpacerIndexFromView(presenter, start);
            }

            if (index < 0)
            {
                return null;
            }

            return new InsertAnchor
            {
                Collection = presenter.Items,
                Index = index
            };
        }

        private static int ReadSelectedSpacerIndex(WorkflowItemsPresenter presenter)
        {
            try
            {
                FieldInfo field = typeof(WorkflowItemsPresenter).GetField(
                    "selectedSpacerIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    return -1;
                }

                object value = field.GetValue(presenter);
                if (value == null)
                {
                    return -1;
                }

                int index = Convert.ToInt32(value);
                return index >= 0 ? index : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static int ReadSpacerIndexFromView(WorkflowItemsPresenter presenter, DependencyObject start)
        {
            try
            {
                DependencyObject current = start;
                while (current != null && !ReferenceEquals(current, presenter))
                {
                    object local = current.ReadLocalValue(WorkflowItemsPresenter.IndexProperty);
                    if (local != DependencyProperty.UnsetValue)
                    {
                        int attached = Convert.ToInt32(local);
                        return attached >= 0 ? attached : -1;
                    }

                    current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
                }

                ItemsControl panel = GetPresenterPanel(presenter);
                if (panel == null)
                {
                    return -1;
                }

                int viewIndex = IndexInItemsControl(panel, start);
                if (!IsSpacerViewIndex(viewIndex))
                {
                    return -1;
                }

                return GetSpacerIndex(viewIndex);
            }
            catch
            {
                return -1;
            }
        }

        private static ItemsControl GetPresenterPanel(WorkflowItemsPresenter presenter)
        {
            try
            {
                FieldInfo field = typeof(WorkflowItemsPresenter).GetField(
                    "panel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                return field != null ? field.GetValue(presenter) as ItemsControl : null;
            }
            catch
            {
                return null;
            }
        }

        private static int IndexInItemsControl(ItemsControl panel, DependencyObject descendant)
        {
            if (panel == null || descendant == null)
            {
                return -1;
            }

            for (int i = 0; i < panel.Items.Count; i++)
            {
                var element = panel.Items[i] as DependencyObject;
                if (element == null)
                {
                    continue;
                }

                if (ReferenceEquals(element, descendant) || IsAncestorOf(element, descendant))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsAncestorOf(DependencyObject ancestor, DependencyObject descendant)
        {
            DependencyObject current = descendant;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsSpacerViewIndex(int viewIndex)
        {
            if (viewIndex == 1)
            {
                return true;
            }

            return viewIndex >= 3 && (viewIndex % 2 == 1);
        }

        private static int GetSpacerIndex(int viewIndex)
        {
            if (viewIndex == 1)
            {
                return 0;
            }

            return (viewIndex - 3) / 2 + 1;
        }

        private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            DependencyObject current = start;
            while (current != null)
            {
                T match = current as T;
                if (match != null)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private sealed class InsertAnchor
        {
            public ModelItemCollection Collection;
            public int Index;
        }
    }
}
