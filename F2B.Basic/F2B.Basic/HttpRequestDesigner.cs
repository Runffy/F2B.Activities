using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    public sealed class HttpRequestDesigner : ActivityDesigner
    {
        private const int LabelWidth = 96;

        private Border _urlBorder;
        private ExpressionTextBox _urlBox;
        private ComboBox _methodCombo;
        private bool _methodProgrammaticChange;

        public HttpRequestDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateExpressionRow("URL", "ModelItem.Url", typeof(string), "https://...", out _urlBorder, out _urlBox, "In", 1, 1));
            panel.Children.Add(CreateMethodRow());
            panel.Children.Add(CreateExpressionRow("Headers", "ModelItem.Headers", typeof(Dictionary<string, string>), "Dictionary variable", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Cookies", "ModelItem.Cookies", typeof(Dictionary<string, string>), "Dictionary variable", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Params", "ModelItem.Params", typeof(Dictionary<string, object>), "Query params dictionary", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("JSON", "ModelItem.Json", typeof(Dictionary<string, object>), "JSON body dictionary", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Body", "ModelItem.Body", typeof(string), "Raw body when JSON empty", out _, out _, "In", 2, 5));
            panel.Children.Add(CreateExpressionRow("Content-Type", "ModelItem.ContentType", typeof(string), "application/json", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Timeout (sec)", "ModelItem.TimeoutSeconds", typeof(double), "100", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Allow redirect", "ModelItem.AllowRedirect", typeof(bool), "True", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Raise on error", "ModelItem.ThrowOnFailure", typeof(bool), "False", out _, out _, "In", 1, 1));
            panel.Children.Add(CreateExpressionRow("Response body", "ModelItem.ResponseBody", typeof(string), "Out variable", out _, out _, "Out", 1, 1));
            panel.Children.Add(CreateExpressionRow("Status code", "ModelItem.StatusCode", typeof(int), "Out variable", out _, out _, "Out", 1, 1));
            panel.Children.Add(CreateExpressionRow("Response headers", "ModelItem.ResponseHeaders", typeof(string), "Out variable", out _, out _, "Out", 1, 1));

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
        }

        private FrameworkElement CreateMethodRow()
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Method",
                Width = LabelWidth,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });

            _methodCombo = new ComboBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                ItemsSource = HttpRequestActivity.GetAllowedHttpMethods(),
            };

            _methodCombo.SelectionChanged += OnMethodComboSelectionChanged;
            row.Children.Add(_methodCombo);
            return row;
        }

        private static FrameworkElement CreateExpressionRow(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            out Border editorBorder,
            out ExpressionTextBox expressionBox,
            string converterParameter,
            int minLines,
            int maxLines)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = LabelWidth,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });

            expressionBox = new ExpressionTextBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                MinLines = minLines,
                MaxLines = maxLines,
            };

            BindingOperations.SetBinding(expressionBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionBox, ExpressionTextBox.ExpressionProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = converterParameter,
            });

            editorBorder = new Border
            {
                Margin = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionBox,
            };

            row.Children.Add(editorBorder);
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            SyncMethodComboFromModel();
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(HttpRequestActivity.Method), StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(new Action(SyncMethodComboFromModel), DispatcherPriority.Background);
            }

            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnMethodComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_methodProgrammaticChange || ModelItem == null || _methodCombo?.SelectedItem == null)
            {
                return;
            }

            ModelItem.Properties[nameof(HttpRequestActivity.Method)].SetValue(_methodCombo.SelectedItem.ToString());
        }

        private void SyncMethodComboFromModel()
        {
            if (_methodCombo == null || ModelItem == null)
            {
                return;
            }

            string m = ReadMethodProperty();
            IReadOnlyList<string> verbs = HttpRequestActivity.GetAllowedHttpMethods();
            string selected =
                verbs.FirstOrDefault(v => string.Equals(v, m, StringComparison.OrdinalIgnoreCase))
                ?? verbs[0];

            _methodProgrammaticChange = true;
            try
            {
                _methodCombo.SelectedItem = selected;
                if (_methodCombo.SelectedItem == null && _methodCombo.Items.Count > 0)
                {
                    _methodCombo.SelectedIndex = 0;
                    ModelItem.Properties[nameof(HttpRequestActivity.Method)].SetValue(_methodCombo.SelectedItem.ToString());
                }
            }
            finally
            {
                _methodProgrammaticChange = false;
            }
        }

        private string ReadMethodProperty()
        {
            IReadOnlyList<string> verbs = HttpRequestActivity.GetAllowedHttpMethods();
            string fallback = verbs[0];
            try
            {
                if (ModelItem?.GetCurrentValue() is HttpRequestActivity hr)
                {
                    string text = hr.Method?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text.ToUpperInvariant();
                    }
                }
            }
            catch
            {
            }

            return fallback;
        }

        private void RefreshRequiredBorders()
        {
            SetRequiredBorder(_urlBorder, IsArgumentFilled(ModelItem, "Url", _urlBox));
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
                string expressionText = expressionProperty.Value.ToString();
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
