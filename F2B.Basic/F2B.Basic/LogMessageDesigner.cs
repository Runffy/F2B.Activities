using OpenRPA.Interfaces;
using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Activities.Presentation.ViewState;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    public sealed class LogMessageDesigner : ActivityDesigner
    {
        private const int MaxIdRefRetryCount = 10;

        private readonly ComboBox levelComboBox;
        private readonly Border messageEditorBorder;
        private readonly ExpressionTextBox messageExpressionBox;
        private bool isSyncingLevel;
        private int idRefRetryCount;
        private bool logEntryIdEnsured;
        private bool viewStateHooked;
        private ViewStateService viewStateService;

        public LogMessageDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateLabeledLevelDropdown(out levelComboBox));

            panel.Children.Add(CreateLabeledExpressionEditor(
                "Message",
                "ModelItem.Message",
                typeof(object),
                "Any object",
                out messageEditorBorder,
                out messageExpressionBox));

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static FrameworkElement CreateLabeledExpressionEditor(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            out Border editorBorder,
            out ExpressionTextBox expressionTextBox)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center
            });

            expressionTextBox = new ExpressionTextBox
            {
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            editorBorder = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionTextBox
            };

            row.Children.Add(editorBorder);
            return row;
        }

        private static FrameworkElement CreateLabeledLevelDropdown(out ComboBox combo)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Level",
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center
            });

            var localCombo = new ComboBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                ItemsSource = new[] { "INFO", "WARN", "ERROR" }
            };

            localCombo.SelectionChanged += (s, e) =>
            {
                if (!(localCombo.Tag is LogMessageDesigner owner) || owner.ModelItem == null || localCombo.SelectedItem == null)
                {
                    return;
                }

                if (owner.isSyncingLevel)
                {
                    return;
                }

                string selectedLevel = localCombo.SelectedItem.ToString();
                string currentLevel = null;
                try
                {
                    currentLevel = owner.ModelItem.GetValue<string>("Level");
                }
                catch
                {
                    // Keep designer resilient; fallback to write value below.
                }

                if (string.Equals((currentLevel ?? string.Empty).Trim(), selectedLevel, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                owner.ModelItem.Properties["Level"].SetValue(new global::System.Activities.InArgument<string>(selectedLevel));
            };
            localCombo.Tag = null;

            row.Children.Add(localCombo);
            combo = localCombo;
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            levelComboBox.Tag = this;
            string current = "INFO";
            try
            {
                var modelValue = ModelItem.GetValue<string>("Level");
                if (!string.IsNullOrWhiteSpace(modelValue))
                {
                    current = modelValue.Trim().ToUpperInvariant();
                }
            }
            catch
            {
                current = "INFO";
            }

            if (current != "INFO" && current != "WARN" && current != "ERROR")
            {
                current = "INFO";
            }

            isSyncingLevel = true;
            levelComboBox.SelectedItem = current;
            isSyncingLevel = false;
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
            HookViewStateService();
            EnsureLogEntryIdIdentity();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnhookViewStateService();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void HookViewStateService()
        {
            if (viewStateHooked)
            {
                return;
            }

            try
            {
                viewStateService = Context?.Services.GetService<ViewStateService>();
                if (viewStateService == null)
                {
                    return;
                }

                viewStateService.ViewStateChanged += OnViewStateChanged;
                viewStateHooked = true;
            }
            catch
            {
                viewStateService = null;
                viewStateHooked = false;
            }
        }

        private void UnhookViewStateService()
        {
            if (!viewStateHooked || viewStateService == null)
            {
                return;
            }

            try
            {
                viewStateService.ViewStateChanged -= OnViewStateChanged;
            }
            catch
            {
                // Ignore detach failures during designer teardown.
            }

            viewStateService = null;
            viewStateHooked = false;
        }

        private void OnViewStateChanged(object sender, ViewStateChangedEventArgs e)
        {
            if (logEntryIdEnsured || e == null || ModelItem == null)
            {
                return;
            }

            if (!string.Equals(e.Key, "IdRef", StringComparison.Ordinal))
            {
                return;
            }

            if (e.ParentModelItem != ModelItem)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(EnsureLogEntryIdIdentity), DispatcherPriority.ContextIdle);
        }

        private void EnsureLogEntryIdIdentity()
        {
            if (logEntryIdEnsured || ModelItem == null)
            {
                return;
            }

            string idRef = TryGetIdRef();
            string currentEntryId = ReadLogEntryId(ModelItem);

            if (string.IsNullOrWhiteSpace(idRef))
            {
                if (idRefRetryCount < MaxIdRefRetryCount)
                {
                    idRefRetryCount++;
                    Dispatcher.BeginInvoke(new Action(EnsureLogEntryIdIdentity), DispatcherPriority.ContextIdle);
                    return;
                }

                // Last resort: backfill empty EntryId even without IdRef so XAML can persist it.
                if (string.IsNullOrWhiteSpace(currentEntryId))
                {
                    string generated = LogMessageActivity.CreateNewLogEntryId();
                    if (TryWriteLogEntryId(generated))
                    {
                        logEntryIdEnsured = true;
                    }
                }
                else
                {
                    logEntryIdEnsured = true;
                }

                return;
            }

            string resolved = LogEntryIdIdentity.Resolve(currentEntryId, idRef, out bool changed);
            if (changed)
            {
                if (!TryWriteLogEntryId(resolved))
                {
                    return;
                }
            }

            LogEntryIdIdentity.Bind(resolved, idRef);
            logEntryIdEnsured = true;
        }

        private bool TryWriteLogEntryId(string value)
        {
            if (ModelItem == null || value == null)
            {
                return false;
            }

            var property = ModelItem.Properties["LogEntryId"];
            if (property == null)
            {
                return false;
            }

            try
            {
                if (property.IsReadOnly)
                {
                    var activity = ModelItem.GetCurrentValue() as LogMessageActivity;
                    if (activity == null)
                    {
                        return false;
                    }

                    activity.LogEntryId = value;
                }

                using (ModelEditingScope scope = ModelItem.BeginEdit("Assign LogEntryId"))
                {
                    property.SetValue(value);
                    scope.Complete();
                }

                string written = ReadLogEntryId(ModelItem);
                return string.Equals((written ?? string.Empty).Trim(), value.Trim(), StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private string TryGetIdRef()
        {
            if (ModelItem == null)
            {
                return null;
            }

            try
            {
                object instance = ModelItem.GetCurrentValue();
                if (instance != null)
                {
                    string attached = WorkflowViewState.GetIdRef(instance);
                    if (!string.IsNullOrWhiteSpace(attached))
                    {
                        return attached.Trim();
                    }
                }
            }
            catch
            {
                // Fall through to ViewStateService.
            }

            try
            {
                var service = viewStateService ?? Context?.Services.GetService<ViewStateService>();
                object value = service?.RetrieveViewState(ModelItem, "IdRef");
                string text = value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
            catch
            {
                // Designer host may not expose view state yet.
            }

            return null;
        }

        private static string ReadLogEntryId(ModelItem modelItem)
        {
            var property = modelItem?.Properties["LogEntryId"];
            if (property == null)
            {
                return null;
            }

            if (property.ComputedValue is string computed)
            {
                return computed;
            }

            if (property.Value != null)
            {
                return property.Value.GetCurrentValue() as string;
            }

            var activity = modelItem.GetCurrentValue() as LogMessageActivity;
            return activity?.LogEntryId;
        }

        private void RefreshRequiredBorders()
        {
            SetRequiredBorder(messageEditorBorder, IsArgumentFilled(ModelItem, "Message", messageExpressionBox));
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
