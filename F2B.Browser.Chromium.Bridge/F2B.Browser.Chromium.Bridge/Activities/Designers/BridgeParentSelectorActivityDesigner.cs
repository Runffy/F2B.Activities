using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeParentSelectorActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly StackPanel _body;
        private readonly Border _parentEditorBorder;
        private Border _selectorEditorBorder;
        private readonly ExpressionTextBox _parentExpressionBox;
        private readonly ExpressionTextBox _selectorExpressionBox;

        private string _selectorPropertyName;
        private bool _selectorRowInitialized;

        public BridgeParentSelectorActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };

            _rootPanel = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5)
            };

            _body = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Grid.SetIsSharedSizeScope(_body, true);

            _parentExpressionBox = CreateExpressionTextBox("ParentObject", typeof(object));
            _body.Children.Add(CreateRow("ParentObject", _parentExpressionBox, out _parentEditorBorder));

            _selectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));

            _rootPanel.Child = _body;
            host.Children.Add(_rootPanel);
            Content = host;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            BindExpressionOwner(_rootPanel, ModelItem);

            _selectorPropertyName = ResolveSelectorPropertyName(ModelItem);
            _selectorExpressionBox.PathToArgument = _selectorPropertyName;
            _selectorExpressionBox.ExpressionType = string.Equals(_selectorPropertyName, "Selectors", StringComparison.Ordinal)
                ? typeof(string[])
                : typeof(string);
            BindingOperations.SetBinding(_selectorExpressionBox, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + _selectorPropertyName)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            EnsureSelectorRow();

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void EnsureSelectorRow()
        {
            if (_selectorRowInitialized)
            {
                return;
            }

            FrameworkElement selectorRow;
            if (string.Equals(_selectorPropertyName, "Selectors", StringComparison.Ordinal))
            {
                selectorRow = SelectorDesignerSupport.CreateSelectorsRow(
                    "Selectors",
                    _selectorExpressionBox,
                    _selectorPropertyName,
                    () => ModelItem,
                    "ParentSelectorLabelColumn",
                    EditorMinWidth,
                    out _selectorEditorBorder,
                    RowSpacing);
            }
            else
            {
                selectorRow = SelectorDesignerSupport.CreateSelectorRow(
                    "Selector",
                    _selectorExpressionBox,
                    _selectorPropertyName,
                    () => ModelItem,
                    "ParentSelectorLabelColumn",
                    EditorMinWidth,
                    out _selectorEditorBorder,
                    RowSpacing);
            }

            _body.Children.Add(selectorRow);
            _selectorRowInitialized = true;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            SetRequiredBorder(_parentEditorBorder, true);
            SetRequiredBorder(_selectorEditorBorder, IsArgumentFilled(ModelItem, _selectorPropertyName, _selectorExpressionBox));
        }

        private static string ResolveSelectorPropertyName(ModelItem modelItem)
        {
            if (modelItem?.Properties == null)
            {
                return "Selector";
            }

            return modelItem.Properties["Selectors"] != null ? "Selectors" : "Selector";
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, out Border editorBorder, double top = 0)
        {
            NormalizeEditor(editor);

            var row = new Grid
            {
                Margin = new Thickness(0, top, 0, 0)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = RowLabelMinWidth,
                SharedSizeGroup = "ParentSelectorLabelColumn"
            });
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            var labelTextBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(labelTextBlock, 0);
            row.Children.Add(labelTextBlock);

            var host = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = EditorMinWidth,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = editor
            };
            editorBorder = host;
            Grid.SetColumn(host, 1);
            row.Children.Add(host);

            return row;
        }

        private static void NormalizeEditor(FrameworkElement editor)
        {
            editor.VerticalAlignment = VerticalAlignment.Center;
            editor.HorizontalAlignment = HorizontalAlignment.Left;

            if (editor is Control control)
            {
                control.FontSize = 12;
                control.MinHeight = 22;
                control.Height = 22;
            }

            if (editor is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.MinWidth = EditorMinWidth;
                expressionTextBox.Width = EditorMinWidth;
                expressionTextBox.MinHeight = 22;
                expressionTextBox.Height = 22;
                expressionTextBox.MaxHeight = 22;
                expressionTextBox.MinLines = 1;
                expressionTextBox.MaxLines = 1;
            }
        }

        private static ExpressionTextBox CreateExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType
            };

            BindingOperations.SetBinding(editor, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(editor, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            return editor;
        }

        private static void BindExpressionOwner(DependencyObject parent, ModelItem owner)
        {
            if (parent is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.OwnerActivity = owner;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
            {
                BindExpressionOwner(VisualTreeHelper.GetChild(parent, i), owner);
            }
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return HasEditorInput(editor);
            }

            if (property.IsSet)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return HasEditorInput(editor);
            }

            if (expressionProperty.Value == null && expressionProperty.ComputedValue == null)
            {
                return HasEditorInput(editor);
            }

            if (expressionProperty.ComputedValue is string text)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            if (expressionProperty.Value != null)
            {
                var expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor != null && editor.Expression != null;
        }

        private static void SetRequiredBorder(Border border, bool filled)
        {
            if (border == null)
            {
                return;
            }

            if (filled)
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }
    }
}
