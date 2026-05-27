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

namespace F2B.Browser.IExplore.COM
{
    public sealed class RequiredFieldsActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly StackPanel _bodyPanel;
        private readonly Dictionary<string, Border> _editorBorders = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ExpressionTextBox> _editors = new Dictionary<string, ExpressionTextBox>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _requiredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> GlobalHiddenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Timeout",
            "Interval",
            "DelayBefore",
            "DelayAfter"
        };
        private static readonly HashSet<string> FramePathVisibleActivities = new HashSet<string>(StringComparer.Ordinal)
        {
            "WaitForFrameActivity",
            "HasFrameActivity"
        };
        private static readonly Dictionary<string, HashSet<string>> ActivityHiddenProperties = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["CheckElementActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TriggerEvents"
            },
            ["InputTextActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TriggerEvents"
            },
            ["SetElementAttributeActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TriggerEvents"
            },
            ["UncheckElementActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TriggerEvents"
            },
            ["ConnectEmbeddedIeWindowActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Hwnd"
            },
            ["GetElementAttributeActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "DefaultValue"
            },
            ["OpenIExploreActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "IExplorePath"
            },
            ["RunJsActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Args"
            },
            ["SelectOptionActivity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Text",
                "Value",
                "Index",
                "TextContains",
                "TextRegex",
                "TriggerEvents",
                "TriggerDoubleClick"
            }
        };
        private bool _initialized;

        public RequiredFieldsActivityDesigner()
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
            _requiredProperties.Clear();

            var activityType = modelItem.ItemType;
            var argumentProperties = ResolveInArgumentProperties(activityType)
                .Where(property => ShouldDisplayProperty(activityType, property))
                .ToList();

            var rowIndex = 0;
            for (var i = 0; i < argumentProperties.Count; i++)
            {
                var property = argumentProperties[i];
                var propertyName = property.Name;
                var expressionType = property.PropertyType.GetGenericArguments()[0];
                var displayName = ResolveDisplayName(property);
                var isRequired = property.GetCustomAttribute<RequiredArgumentAttribute>() != null || IsCustomRequired(activityType, propertyName);

                var editor = CreateExpressionTextBox(propertyName, expressionType);
                var row = CreateRow(displayName, editor, out var editorBorder, rowIndex == 0 ? 0 : RowSpacing);

                _bodyPanel.Children.Add(row);
                _editors[propertyName] = editor;
                _editorBorders[propertyName] = editorBorder;
                if (isRequired)
                {
                    _requiredProperties.Add(propertyName);
                }

                rowIndex++;
            }
        }

        private static bool ShouldDisplayProperty(Type activityType, PropertyInfo property)
        {
            var propertyName = property.Name;
            if (GlobalHiddenProperties.Contains(propertyName))
            {
                return false;
            }

            if (string.Equals(propertyName, "FramePath", StringComparison.OrdinalIgnoreCase)
                && !FramePathVisibleActivities.Contains(activityType.Name))
            {
                return false;
            }

            if (ActivityHiddenProperties.TryGetValue(activityType.Name, out var hiddenProperties)
                && hiddenProperties.Contains(propertyName))
            {
                return false;
            }

            return true;
        }

        private static bool IsCustomRequired(Type activityType, string propertyName)
        {
            return string.Equals(activityType.Name, "SetElementAttributeActivity", StringComparison.Ordinal)
                && string.Equals(propertyName, "Value", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<PropertyInfo> ResolveInArgumentProperties(Type activityType)
        {
            return activityType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(InArgument<>))
                .OrderBy(p => ResolveCategoryOrder(p))
                .ThenBy(p => p.MetadataToken);
        }

        private static int ResolveCategoryOrder(PropertyInfo property)
        {
            var category = property.GetCustomAttribute<CategoryAttribute>();
            if (category == null || string.IsNullOrWhiteSpace(category.Category))
            {
                return 99;
            }

            if (string.Equals(category.Category, "Target", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(category.Category, "Input", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(category.Category, "Filter", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(category.Category, "Select", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(category.Category, "Time", StringComparison.OrdinalIgnoreCase)) return 5;
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
                SharedSizeGroup = "IERequiredFieldLabelColumn"
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

            foreach (var propertyName in _requiredProperties)
            {
                if (!_editorBorders.TryGetValue(propertyName, out var border))
                {
                    continue;
                }

                _editors.TryGetValue(propertyName, out var editor);
                var filled = IsArgumentFilled(ModelItem, propertyName, editor);
                SetRequiredBorder(border, required: true, filled: filled);
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

        private static void SetRequiredBorder(Border border, bool required, bool filled)
        {
            if (border == null)
            {
                return;
            }

            if (!required || filled)
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

            var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < childrenCount; i++)
            {
                BindExpressionOwner(System.Windows.Media.VisualTreeHelper.GetChild(current, i), owner);
            }
        }
    }
}
