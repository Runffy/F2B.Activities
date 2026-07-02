using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.File
{
    /// <summary>
    /// Shows only required InArgument inputs on the workflow canvas; optional inputs stay in the property grid.
    /// </summary>
    public sealed class FileRequiredFieldsActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly StackPanel _bodyPanel;
        private readonly Dictionary<string, Border> _editorBorders = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ExpressionTextBox> _editors = new Dictionary<string, ExpressionTextBox>(StringComparer.OrdinalIgnoreCase);
        private bool _initialized;

        public FileRequiredFieldsActivityDesigner()
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

            _bodyPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Grid.SetIsSharedSizeScope(_bodyPanel, true);

            _rootPanel.Child = _bodyPanel;
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

            if (!_initialized)
            {
                BuildRows(ModelItem);
                _initialized = true;
                ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            }

            BindExpressionOwner(_rootPanel, ModelItem);
            RefreshRequiredBorders();
        }

        private void BuildRows(ModelItem modelItem)
        {
            _bodyPanel.Children.Clear();
            _editorBorders.Clear();
            _editors.Clear();

            var argumentProperties = ResolveRequiredInArgumentProperties(modelItem.ItemType).ToList();
            RefreshCanvasVisibility(argumentProperties.Count > 0);

            for (var i = 0; i < argumentProperties.Count; i++)
            {
                var property = argumentProperties[i];
                var propertyName = property.Name;
                var expressionType = property.PropertyType.GetGenericArguments()[0];
                var displayName = ResolveDisplayName(property);

                var editor = CreateExpressionTextBox(propertyName, expressionType);
                var row = CreateRow(displayName, editor, out var editorBorder, i == 0 ? 0 : RowSpacing);

                _bodyPanel.Children.Add(row);
                _editors[propertyName] = editor;
                _editorBorders[propertyName] = editorBorder;
            }
        }

        private void RefreshCanvasVisibility(bool hasVisibleRows)
        {
            _rootPanel.Visibility = hasVisibleRows ? Visibility.Visible : Visibility.Collapsed;
            if (Content is StackPanel host)
            {
                host.Margin = hasVisibleRows ? new Thickness(4) : new Thickness(0);
            }
        }

        private static IEnumerable<PropertyInfo> ResolveRequiredInArgumentProperties(Type activityType)
        {
            return activityType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p =>
                    p.CanRead
                    && p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition() == typeof(InArgument<>)
                    && p.GetCustomAttribute<RequiredArgumentAttribute>() != null)
                .OrderBy(ResolveCategoryOrder)
                .ThenBy(p => p.MetadataToken);
        }

        private static int ResolveCategoryOrder(PropertyInfo property)
        {
            var category = property.GetCustomAttribute<CategoryAttribute>();
            if (category == null || string.IsNullOrWhiteSpace(category.Category))
            {
                return 99;
            }

            if (category.Category.StartsWith("Input.", StringComparison.OrdinalIgnoreCase)
                && category.Category.Length == "Input.X".Length
                && char.IsLetter(category.Category[category.Category.Length - 1]))
            {
                return category.Category[category.Category.Length - 1] - 'A' + 1;
            }

            return 10;
        }

        private static string ResolveDisplayName(PropertyInfo property)
        {
            var displayName = property.GetCustomAttribute<DisplayNameAttribute>();
            return displayName != null && !string.IsNullOrWhiteSpace(displayName.DisplayName)
                ? displayName.DisplayName
                : property.Name;
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
                SharedSizeGroup = "FileRequiredFieldLabelColumn"
            });
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            var labelTextBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                ToolTip = label
            };
            Grid.SetColumn(labelTextBlock, 0);
            row.Children.Add(labelTextBlock);

            var host = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = EditorMinWidth,
                Width = EditorMinWidth,
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
            }
        }

        private static ExpressionTextBox CreateExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
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

            foreach (var pair in _editorBorders)
            {
                _editors.TryGetValue(pair.Key, out var editor);
                var filled = IsArgumentFilled(ModelItem, pair.Key, editor);
                SetRequiredBorder(pair.Value, filled);
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

            if (property.ComputedValue != null)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
            }

            var propertyValueText = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(propertyValueText) && !string.Equals(propertyValueText, "null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
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

        private static void BindExpressionOwner(DependencyObject current, ModelItem owner)
        {
            if (current == null || owner == null)
            {
                return;
            }

            if (current is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.OwnerActivity = owner;
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < childrenCount; i++)
            {
                BindExpressionOwner(VisualTreeHelper.GetChild(current, i), owner);
            }
        }
    }
}
