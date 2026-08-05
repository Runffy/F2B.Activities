using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    public sealed class MultipleAssignDesigner : ActivityDesigner
    {
        private readonly StackPanel _rowsPanel;
        private readonly Button _addButton;
        private ModelItemCollection _assignments;
        private bool _rebuilding;
        private bool _suppressCollectionRebuild;

        public MultipleAssignDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                MinWidth = 320
            };

            var root = new StackPanel();
            _rowsPanel = new StackPanel();
            root.Children.Add(_rowsPanel);

            _addButton = new Button
            {
                Content = "Add",
                Width = 72,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 2, 8, 2)
            };
            _addButton.Click += OnAddClicked;
            root.Children.Add(_addButton);

            border.Child = root;
            Content = border;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            EnsureAssignmentsCollection();
            RebuildRows();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachCollectionChanged();
        }

        private void EnsureAssignmentsCollection()
        {
            DetachCollectionChanged();

            var property = ModelItem.Properties["Assignments"];
            _assignments = property?.Collection;
            if (_assignments == null)
            {
                return;
            }

            if (_assignments.Count == 0)
            {
                using (ModelEditingScope scope = ModelItem.BeginEdit("Initialize Assignments"))
                {
                    _assignments.Add(new AssignEntry());
                    scope.Complete();
                }
            }

            _assignments.CollectionChanged += OnAssignmentsCollectionChanged;
        }

        private void DetachCollectionChanged()
        {
            if (_assignments != null)
            {
                _assignments.CollectionChanged -= OnAssignmentsCollectionChanged;
                _assignments = null;
            }
        }

        private void OnAssignmentsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_rebuilding || _suppressCollectionRebuild)
            {
                return;
            }

            CommitExpressionEdits();
            RebuildRows();
        }

        private void RebuildRows()
        {
            if (ModelItem == null)
            {
                return;
            }

            if (_assignments == null)
            {
                EnsureAssignmentsCollection();
            }

            if (_assignments == null)
            {
                return;
            }

            _rebuilding = true;
            try
            {
                _rowsPanel.Children.Clear();
                bool canDelete = _assignments.Count > 1;

                for (int i = 0; i < _assignments.Count; i++)
                {
                    ModelItem entryItem = _assignments[i];
                    _rowsPanel.Children.Add(CreateAssignmentRow(entryItem, canDelete));
                }
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private FrameworkElement CreateAssignmentRow(ModelItem entryItem, bool canDelete)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2), Tag = entryItem };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Type assignType = ResolveAssignType(entryItem);

            Border toBorder;
            ExpressionTextBox toBox = CreateArgumentEditor(
                entryItem,
                "To",
                "To",
                useLocationExpression: true,
                converterParameter: "Out",
                assignType,
                out toBorder);
            Grid.SetColumn(toBorder, 0);
            row.Children.Add(toBorder);

            var equals = new TextBlock
            {
                Text = "=",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(equals, 1);
            row.Children.Add(equals);

            Border valueBorder;
            ExpressionTextBox valueBox = CreateArgumentEditor(
                entryItem,
                "Value",
                "Value",
                useLocationExpression: false,
                converterParameter: "In",
                assignType,
                out valueBorder);
            Grid.SetColumn(valueBorder, 2);
            row.Children.Add(valueBorder);

            var deleteButton = new Button
            {
                Content = "X",
                Width = 28,
                Height = 24,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = canDelete,
                Tag = entryItem,
                ToolTip = canDelete ? "Delete row" : "At least one assignment row is required"
            };
            deleteButton.Click += OnDeleteClicked;
            Grid.SetColumn(deleteButton, 3);
            row.Children.Add(deleteButton);

            // Keep both ExpressionTypes aligned with the target variable / argument type.
            Action syncType = () => SyncRowExpressionTypes(entryItem, toBox, valueBox);
            toBox.LostKeyboardFocus += (s, e) =>
            {
                syncType();
                RefreshToBorder(toBorder, entryItem, toBox);
            };
            valueBox.LostKeyboardFocus += (s, e) => syncType();
            toBox.LostFocus += (s, e) =>
            {
                syncType();
                RefreshToBorder(toBorder, entryItem, toBox);
            };
            valueBox.LostFocus += (s, e) => syncType();

            HookInnerTextChanged(toBox, () =>
            {
                syncType();
                RefreshToBorder(toBorder, entryItem, toBox);
            });
            HookInnerTextChanged(valueBox, syncType);

            toBox.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    syncType();
                    RefreshToBorder(toBorder, entryItem, toBox);
                }),
                DispatcherPriority.Loaded);

            return row;
        }

        private ExpressionTextBox CreateArgumentEditor(
            ModelItem entryItem,
            string propertyName,
            string hint,
            bool useLocationExpression,
            string converterParameter,
            Type expressionType,
            out Border host)
        {
            var expressionTextBox = new ExpressionTextBox
            {
                HintText = hint,
                // Must match the row's assign type (String/Int32/…) — object alone cannot L-value an Int32 var.
                ExpressionType = expressionType ?? typeof(object),
                UseLocationExpression = useLocationExpression,
                PathToArgument = propertyName,
                MinLines = 1,
                MaxLines = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 100
            };

            BindingOperations.SetBinding(
                expressionTextBox,
                ExpressionTextBox.OwnerActivityProperty,
                new Binding("ModelItem") { Source = this });

            BindingOperations.SetBinding(
                expressionTextBox,
                ExpressionTextBox.ExpressionProperty,
                new Binding(propertyName)
                {
                    Source = entryItem,
                    Mode = BindingMode.TwoWay,
                    Converter = new ArgumentToExpressionConverter(),
                    ConverterParameter = converterParameter
                });

            host = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionTextBox,
                VerticalAlignment = VerticalAlignment.Center
            };

            return expressionTextBox;
        }

        private void SyncRowExpressionTypes(ModelItem entryItem, ExpressionTextBox toBox, ExpressionTextBox valueBox)
        {
            if (entryItem == null || toBox == null || valueBox == null)
            {
                return;
            }

            Type assignType = ResolveAssignType(entryItem, toBox, valueBox);
            if (assignType == null)
            {
                assignType = typeof(object);
            }

            if (toBox.ExpressionType != assignType)
            {
                toBox.ExpressionType = assignType;
            }

            if (valueBox.ExpressionType != assignType)
            {
                valueBox.ExpressionType = assignType;
            }
        }

        private Type ResolveAssignType(
            ModelItem entryItem,
            ExpressionTextBox toBox = null,
            ExpressionTextBox valueBox = null)
        {
            // Prefer To location type (variable / existing OutArgument<T>).
            Type fromToArgument = GetArgumentType(entryItem, "To");
            if (fromToArgument != null && fromToArgument != typeof(object))
            {
                return fromToArgument;
            }

            Type fromToExpression = GetExpressionActivityType(toBox?.Expression);
            if (fromToExpression != null && fromToExpression != typeof(object))
            {
                return fromToExpression;
            }

            Type fromToText = ResolveIdentifierType(GetEditorText(toBox));
            if (fromToText != null && fromToText != typeof(object))
            {
                return fromToText;
            }

            Type fromValueArgument = GetArgumentType(entryItem, "Value");
            if (fromValueArgument != null && fromValueArgument != typeof(object))
            {
                return fromValueArgument;
            }

            Type fromValueExpression = GetExpressionActivityType(valueBox?.Expression);
            if (fromValueExpression != null && fromValueExpression != typeof(object))
            {
                return fromValueExpression;
            }

            return typeof(object);
        }

        private static Type GetArgumentType(ModelItem entryItem, string propertyName)
        {
            var property = entryItem?.Properties[propertyName];
            if (property == null)
            {
                return null;
            }

            var argument = property.ComputedValue as Argument;
            if (argument != null && argument.ArgumentType != null)
            {
                return argument.ArgumentType;
            }

            ModelItem argumentItem = property.Value;
            if (argumentItem == null)
            {
                return null;
            }

            Type itemType = argumentItem.ItemType;
            if (itemType != null && itemType.IsGenericType)
            {
                Type definition = itemType.GetGenericTypeDefinition();
                if (definition == typeof(OutArgument<>)
                    || definition == typeof(InArgument<>)
                    || definition == typeof(InOutArgument<>))
                {
                    return itemType.GetGenericArguments()[0];
                }
            }

            return GetExpressionActivityType(argumentItem.Properties["Expression"]?.Value);
        }

        private static Type GetExpressionActivityType(ModelItem expressionItem)
        {
            if (expressionItem == null)
            {
                return null;
            }

            var resultType = expressionItem.Properties["ResultType"]?.ComputedValue as Type;
            if (resultType != null)
            {
                return UnwrapLocationType(resultType);
            }

            Type itemType = expressionItem.ItemType;
            if (itemType != null && itemType.IsGenericType)
            {
                Type[] args = itemType.GetGenericArguments();
                if (args.Length == 1)
                {
                    return UnwrapLocationType(args[0]);
                }
            }

            return null;
        }

        private static Type UnwrapLocationType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Location<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        private Type ResolveIdentifierType(string expressionText)
        {
            if (string.IsNullOrWhiteSpace(expressionText) || ModelItem == null)
            {
                return null;
            }

            string name = expressionText.Trim();
            if (name.IndexOfAny(new[] { ' ', '.', '(', ')', '[', ']', '"', '\'' }) >= 0)
            {
                return null;
            }

            for (ModelItem current = ModelItem; current != null; current = current.Parent)
            {
                ModelItemCollection variables = current.Properties["Variables"]?.Collection;
                if (variables == null)
                {
                    continue;
                }

                foreach (ModelItem variableItem in variables)
                {
                    string variableName = variableItem.Properties["Name"]?.ComputedValue as string;
                    if (!string.Equals(variableName, name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Type variableType = GetVariableType(variableItem);
                    if (variableType != null)
                    {
                        return variableType;
                    }
                }
            }

            return null;
        }

        private static Type GetVariableType(ModelItem variableItem)
        {
            if (variableItem == null)
            {
                return null;
            }

            var typed = variableItem.GetCurrentValue() as Variable;
            if (typed != null && typed.Type != null)
            {
                return typed.Type;
            }

            Type itemType = variableItem.ItemType;
            if (itemType != null && itemType.IsGenericType
                && itemType.GetGenericTypeDefinition() == typeof(Variable<>))
            {
                return itemType.GetGenericArguments()[0];
            }

            return variableItem.Properties["Type"]?.ComputedValue as Type;
        }

        private static string GetEditorText(ExpressionTextBox box)
        {
            if (box == null)
            {
                return null;
            }

            TextBox inner = FindDescendant<TextBox>(box);
            return inner?.Text;
        }

        private static void HookInnerTextChanged(ExpressionTextBox box, Action onChanged)
        {
            if (box == null || onChanged == null)
            {
                return;
            }

            box.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    TextBox inner = FindDescendant<TextBox>(box);
                    if (inner == null)
                    {
                        return;
                    }

                    inner.TextChanged += (s, e) => onChanged();
                }),
                DispatcherPriority.Loaded);
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            if (root is T match)
            {
                return match;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                T child = FindDescendant<T>(VisualTreeHelper.GetChild(root, i));
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null || _assignments == null)
            {
                return;
            }

            CommitExpressionEdits();

            _suppressCollectionRebuild = true;
            try
            {
                using (ModelEditingScope scope = ModelItem.BeginEdit("Add Assignment"))
                {
                    _assignments.Add(new AssignEntry());
                    scope.Complete();
                }

                ModelItem newEntry = _assignments[_assignments.Count - 1];
                _rowsPanel.Children.Add(CreateAssignmentRow(newEntry, canDelete: true));
                RefreshDeleteButtons();
            }
            finally
            {
                _suppressCollectionRebuild = false;
            }
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null || _assignments == null || _assignments.Count <= 1)
            {
                return;
            }

            var button = sender as Button;
            var entryItem = button?.Tag as ModelItem;
            if (entryItem == null)
            {
                return;
            }

            CommitExpressionEdits();

            _suppressCollectionRebuild = true;
            try
            {
                using (ModelEditingScope scope = ModelItem.BeginEdit("Delete Assignment"))
                {
                    _assignments.Remove(entryItem);
                    if (_assignments.Count == 0)
                    {
                        _assignments.Add(new AssignEntry());
                    }

                    scope.Complete();
                }

                RemoveRowForEntry(entryItem);

                if (_rowsPanel.Children.Count != _assignments.Count)
                {
                    RebuildRows();
                }
                else
                {
                    RefreshDeleteButtons();
                }
            }
            finally
            {
                _suppressCollectionRebuild = false;
            }
        }

        private void RemoveRowForEntry(ModelItem entryItem)
        {
            for (int i = _rowsPanel.Children.Count - 1; i >= 0; i--)
            {
                var row = _rowsPanel.Children[i] as FrameworkElement;
                if (row != null && ReferenceEquals(row.Tag, entryItem))
                {
                    _rowsPanel.Children.RemoveAt(i);
                    return;
                }
            }
        }

        private void RefreshDeleteButtons()
        {
            bool canDelete = _assignments != null && _assignments.Count > 1;
            foreach (object child in _rowsPanel.Children)
            {
                var row = child as Grid;
                if (row == null)
                {
                    continue;
                }

                foreach (object element in row.Children)
                {
                    var deleteButton = element as Button;
                    if (deleteButton == null || !string.Equals(deleteButton.Content as string, "X", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    deleteButton.IsEnabled = canDelete;
                    deleteButton.ToolTip = canDelete
                        ? "Delete row"
                        : "At least one assignment row is required";
                }
            }
        }

        private void CommitExpressionEdits()
        {
            try
            {
                if (Keyboard.FocusedElement is UIElement focused)
                {
                    focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
            catch
            {
                // Ignore focus traversal failures; still try UpdateSource below.
            }

            Keyboard.ClearFocus();
            CommitExpressionBindings(_rowsPanel);
        }

        private static void CommitExpressionBindings(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            var expressionTextBox = root as ExpressionTextBox;
            if (expressionTextBox != null)
            {
                BindingExpression binding = expressionTextBox.GetBindingExpression(ExpressionTextBox.ExpressionProperty);
                if (binding != null)
                {
                    binding.UpdateSource();
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                CommitExpressionBindings(VisualTreeHelper.GetChild(root, i));
            }
        }

        private static void RefreshToBorder(Border border, ModelItem entryItem, ExpressionTextBox editor = null)
        {
            if (border == null)
            {
                return;
            }

            if (IsArgumentFilled(entryItem, "To", editor))
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }

        private static bool IsArgumentFilled(ModelItem entryItem, string propertyName, ExpressionTextBox editor = null)
        {
            if (editor != null)
            {
                if (editor.Expression != null)
                {
                    return true;
                }

                string text = GetEditorText(editor);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return true;
                }
            }

            var property = entryItem?.Properties[propertyName];
            if (property == null)
            {
                return false;
            }

            if (property.IsSet && property.Value != null)
            {
                var expressionProperty = property.Value.Properties["Expression"];
                if (expressionProperty != null
                    && (expressionProperty.Value != null || expressionProperty.ComputedValue != null))
                {
                    return true;
                }

                return true;
            }

            return false;
        }
    }
}
