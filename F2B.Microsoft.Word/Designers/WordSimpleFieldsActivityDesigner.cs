using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    /// <summary>
    /// Canvas designer for simple Word activities: required InArguments plus Document Path/Document fields when present.
    /// </summary>
    public sealed class WordSimpleFieldsActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordSimpleFieldLabelColumn";

        private readonly Border _rootPanel;
        private readonly StackPanel _bodyPanel;
        private readonly Dictionary<string, Border> _editorBorders = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ExpressionTextBox> _editors = new Dictionary<string, ExpressionTextBox>(StringComparer.OrdinalIgnoreCase);
        private bool _initialized;

        public WordSimpleFieldsActivityDesigner()
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

            _bodyPanel = new StackPanel { Orientation = Orientation.Vertical };
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

            WordDesignerShared.BindExpressionOwner(_rootPanel, ModelItem);
            RefreshRequiredBorders();
        }

        private void BuildRows(ModelItem modelItem)
        {
            _bodyPanel.Children.Clear();
            _editorBorders.Clear();
            _editors.Clear();

            var fields = ResolveCanvasFields(modelItem.ItemType).ToList();
            var top = 0d;
            foreach (var field in fields)
            {
                ExpressionTextBox editor;
                if (field.Direction == ArgumentDirection.Out)
                {
                    editor = WordDesignerShared.CreateOutExpressionTextBox(field.Name, field.ExpressionType);
                }
                else if (field.Direction == ArgumentDirection.InOut)
                {
                    editor = WordDesignerShared.CreateInOutExpressionTextBox(field.Name, field.ExpressionType);
                }
                else
                {
                    editor = WordDesignerShared.CreateInExpressionTextBox(field.Name, field.ExpressionType);
                }

                var row = WordDesignerShared.CreateRow(field.DisplayName, editor, LabelColumn, out var border, top);
                top = WordDesignerShared.RowSpacing;
                _bodyPanel.Children.Add(row);
                _editors[field.Name] = editor;
                _editorBorders[field.Name] = border;
            }
        }

        private static IEnumerable<CanvasField> ResolveCanvasFields(Type activityType)
        {
            foreach (var property in activityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(ResolveCategoryOrder)
                .ThenBy(p => p.MetadataToken))
            {
                if (!property.CanRead || !property.PropertyType.IsGenericType)
                {
                    continue;
                }

                var generic = property.PropertyType.GetGenericTypeDefinition();
                ArgumentDirection direction;
                if (generic == typeof(InArgument<>))
                {
                    direction = ArgumentDirection.In;
                }
                else if (generic == typeof(OutArgument<>))
                {
                    direction = ArgumentDirection.Out;
                }
                else if (generic == typeof(InOutArgument<>))
                {
                    direction = ArgumentDirection.InOut;
                }
                else
                {
                    continue;
                }

                var expressionType = property.PropertyType.GetGenericArguments()[0];
                var required = property.GetCustomAttribute<RequiredArgumentAttribute>() != null;
                var isDocument = string.Equals(property.Name, "Document", StringComparison.Ordinal)
                    || expressionType == typeof(InteropWord.Document);
                var isPath = string.Equals(property.Name, "WordFilePath", StringComparison.OrdinalIgnoreCase);

                if (!required && !isDocument && !isPath)
                {
                    continue;
                }

                var display = property.GetCustomAttribute<DisplayNameAttribute>();
                yield return new CanvasField
                {
                    Name = property.Name,
                    DisplayName = display != null && !string.IsNullOrWhiteSpace(display.DisplayName)
                        ? display.DisplayName
                        : property.Name,
                    ExpressionType = expressionType,
                    Direction = direction,
                    Required = required
                };
            }
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

            if (string.Equals(category.Category, "Output", StringComparison.OrdinalIgnoreCase))
            {
                return 80;
            }

            return 10;
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
                var required = ModelItem.ItemType.GetProperty(pair.Key)
                    ?.GetCustomAttribute<RequiredArgumentAttribute>() != null;
                if (!required)
                {
                    WordDesignerShared.SetRequiredBorder(pair.Value, true);
                    continue;
                }

                WordDesignerShared.SetRequiredBorder(
                    pair.Value,
                    WordDesignerShared.IsArgumentFilled(ModelItem, pair.Key, editor));
            }
        }

        private sealed class CanvasField
        {
            public string Name { get; set; }

            public string DisplayName { get; set; }

            public Type ExpressionType { get; set; }

            public ArgumentDirection Direction { get; set; }

            public bool Required { get; set; }
        }
    }
}
