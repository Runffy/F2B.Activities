using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace F2B.Basic
{
    public sealed class MultipleAssignDesigner : ActivityDesigner
    {
        private readonly StackPanel _rowsPanel;
        private readonly Button _addButton;
        private ModelItemCollection _assignments;
        private bool _rebuilding;

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
            if (_rebuilding)
            {
                return;
            }

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
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 100 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border toBorder;
            ExpressionTextBox toBox = CreateArgumentEditor(
                entryItem,
                "To",
                "To",
                useLocationExpression: true,
                converterParameter: "Out",
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
            CreateArgumentEditor(
                entryItem,
                "Value",
                "Value",
                useLocationExpression: false,
                converterParameter: "In",
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

            toBox.LostFocus += (s, e) => RefreshToBorder(toBorder, entryItem);
            RefreshToBorder(toBorder, entryItem);

            return row;
        }

        private ExpressionTextBox CreateArgumentEditor(
            ModelItem entryItem,
            string propertyName,
            string hint,
            bool useLocationExpression,
            string converterParameter,
            out Border host)
        {
            var expressionTextBox = new ExpressionTextBox
            {
                HintText = hint,
                ExpressionType = typeof(object),
                UseLocationExpression = useLocationExpression,
                PathToArgument = propertyName,
                MinLines = 1,
                MaxLines = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 100
            };

            // OwnerActivity must be the activity ModelItem (expression scope), not the entry.
            BindingOperations.SetBinding(
                expressionTextBox,
                ExpressionTextBox.OwnerActivityProperty,
                new Binding("ModelItem") { Source = this });

            BindingOperations.SetBinding(
                expressionTextBox,
                ExpressionTextBox.ExpressionProperty,
                new Binding("Properties[" + propertyName + "].Value")
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

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null || _assignments == null)
            {
                return;
            }

            using (ModelEditingScope scope = ModelItem.BeginEdit("Add Assignment"))
            {
                _assignments.Add(new AssignEntry());
                scope.Complete();
            }

            RebuildRows();
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

            using (ModelEditingScope scope = ModelItem.BeginEdit("Delete Assignment"))
            {
                _assignments.Remove(entryItem);
                if (_assignments.Count == 0)
                {
                    _assignments.Add(new AssignEntry());
                }

                scope.Complete();
            }

            RebuildRows();
        }

        private static void RefreshToBorder(Border border, ModelItem entryItem)
        {
            if (border == null)
            {
                return;
            }

            if (IsArgumentFilled(entryItem, "To"))
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }

        private static bool IsArgumentFilled(ModelItem entryItem, string propertyName)
        {
            var property = entryItem?.Properties[propertyName];
            if (property == null || !property.IsSet || property.Value == null)
            {
                return false;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return false;
            }

            if (expressionProperty.Value == null && expressionProperty.ComputedValue == null)
            {
                return false;
            }

            if (expressionProperty.ComputedValue is string text)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            return expressionProperty.Value != null
                && !string.IsNullOrWhiteSpace(expressionProperty.Value.ToString());
        }
    }
}
