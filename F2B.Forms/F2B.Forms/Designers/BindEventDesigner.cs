using System;
using System.Collections.Generic;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using F2B.Forms.Engine;
using F2B.Forms.Model;

namespace F2B.Forms.Designers
{
    public sealed class BindEventDesigner : ActivityDesigner
    {
        private readonly ComboBox _controlIdCombo;
        private readonly ComboBox _controlTypeCombo;
        private readonly ComboBox _eventNameCombo;
        private readonly ComboBox _uiBehaviorCombo;
        private readonly TextBlock _statusText;
        private readonly Button _refreshButton;
        private bool _suppressUi;
        private List<FormEventCatalog.ControlRef> _controls = new List<FormEventCatalog.ControlRef>();

        public BindEventDesigner()
        {
            var root = new StackPanel();

            _statusText = new TextBlock
            {
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            root.Children.Add(_statusText);

            root.Children.Add(CreateLabel("Control Id"));
            _controlIdCombo = CreateEditableCombo();
            _controlIdCombo.SelectionChanged += OnControlIdSelectionChanged;
            _controlIdCombo.LostFocus += (s, e) => OnControlIdLostFocus();
            root.Children.Add(_controlIdCombo);

            root.Children.Add(CreateLabel("Control Type"));
            _controlTypeCombo = CreateFixedCombo();
            foreach (string type in FormEventCatalog.GetBindableControlTypes())
            {
                _controlTypeCombo.Items.Add(type);
            }

            _controlTypeCombo.SelectionChanged += OnControlTypeSelectionChanged;
            root.Children.Add(_controlTypeCombo);

            root.Children.Add(CreateLabel("Event Name"));
            _eventNameCombo = CreateFixedCombo();
            _eventNameCombo.SelectionChanged += (s, e) =>
            {
                if (!_suppressUi && _eventNameCombo.SelectedItem != null)
                {
                    CommitLiteral("EventName", Convert.ToString(_eventNameCombo.SelectedItem));
                }
            };
            root.Children.Add(_eventNameCombo);

            root.Children.Add(CreateLabel("UI Behavior"));
            _uiBehaviorCombo = CreateFixedCombo();
            _uiBehaviorCombo.Items.Add("NoLock");
            _uiBehaviorCombo.Items.Add("LockQueue");
            _uiBehaviorCombo.Items.Add("LockIgnore");
            _uiBehaviorCombo.SelectionChanged += (s, e) =>
            {
                if (!_suppressUi && _uiBehaviorCombo.SelectedItem != null)
                {
                    CommitLiteral("UiBehavior", Convert.ToString(_uiBehaviorCombo.SelectedItem));
                }
            };
            root.Children.Add(_uiBehaviorCombo);

            _refreshButton = new Button
            {
                Content = "Refresh from Form JSON",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _refreshButton.Click += (s, e) => ReloadFromFormJson();
            root.Children.Add(_refreshButton);

            root.Children.Add(new TextBlock
            {
                Text = "Handler",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop handler activities here",
                MinWidth = 280,
                MinHeight = 40
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding("ModelItem.Handler")
            {
                Mode = BindingMode.TwoWay
            });
            root.Children.Add(presenter);

            Content = new Border
            {
                BorderBrush = Brushes.SteelBlue,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                MinWidth = 320,
                Child = root
            };

            Loaded += (s, e) =>
            {
                HookModelItem();
                ReloadFromFormJson();
                SyncCombosFromModel();
                EnsureUiBehaviorDefault();
            };
        }

        private ModelItem _asyncFormItem;

        private void HookModelItem()
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged -= OnModelItemPropertyChanged;
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;

            if (_asyncFormItem != null)
            {
                _asyncFormItem.PropertyChanged -= OnAsyncFormPropertyChanged;
            }

            _asyncFormItem = DesignTimeFormPath.FindAsyncForm(ModelItem);
            if (_asyncFormItem != null)
            {
                _asyncFormItem.PropertyChanged += OnAsyncFormPropertyChanged;
            }
        }

        private void OnAsyncFormPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "FormPath")
            {
                ReloadFromFormJson();
            }
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ControlId"
                || e.PropertyName == "ControlType"
                || e.PropertyName == "EventName"
                || e.PropertyName == "UiBehavior")
            {
                SyncCombosFromModel();
            }
        }

        private void ReloadFromFormJson()
        {
            if (ModelItem == null)
            {
                return;
            }

            _suppressUi = true;
            try
            {
                if (DesignTimeFormPath.TryLoadForm(ModelItem, out FormDefinition definition, out string status))
                {
                    _controls = FormEventCatalog.CollectControls(definition);
                    _controlIdCombo.Items.Clear();
                    foreach (FormEventCatalog.ControlRef control in _controls)
                    {
                        _controlIdCombo.Items.Add(control);
                    }

                    _statusText.Text = status;
                    _statusText.Foreground = Brushes.DarkGreen;
                }
                else
                {
                    _controls = new List<FormEventCatalog.ControlRef>();
                    _controlIdCombo.Items.Clear();
                    _statusText.Text = status;
                    _statusText.Foreground = Brushes.DarkOrange;
                }
            }
            finally
            {
                _suppressUi = false;
            }

            SyncCombosFromModel();
        }

        private void OnControlIdSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUi)
            {
                return;
            }

            string controlId = NormalizeControlIdInput(GetControlIdText());
            CommitLiteral("ControlId", controlId);

            if (_controlIdCombo.SelectedItem is FormEventCatalog.ControlRef controlRef
                && !string.IsNullOrWhiteSpace(controlRef.Type))
            {
                ApplyControlType(controlRef.Type, selectFirstEvent: true);
                return;
            }

            // Free-text / unknown id — keep current Control Type; only refresh events if type already chosen.
            RefreshEventNames(selectFirstIfMissing: false);
        }

        private void OnControlIdLostFocus()
        {
            if (_suppressUi)
            {
                return;
            }

            string controlId = NormalizeControlIdInput(GetControlIdText());
            CommitLiteral("ControlId", controlId);

            FormEventCatalog.ControlRef match = FindControlById(controlId);
            if (match != null && !string.IsNullOrWhiteSpace(match.Type))
            {
                ApplyControlType(match.Type, selectFirstEvent: true);
                // Re-select matching item so dropdown shows the ControlRef entry.
                _suppressUi = true;
                try
                {
                    SetControlIdCombo(controlId);
                }
                finally
                {
                    _suppressUi = false;
                }
            }
        }

        private void OnControlTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressUi)
            {
                return;
            }

            string type = _controlTypeCombo.SelectedItem == null
                ? string.Empty
                : Convert.ToString(_controlTypeCombo.SelectedItem);
            CommitLiteral("ControlType", type);
            RefreshEventNames(selectFirstIfMissing: true);
        }

        private void ApplyControlType(string type, bool selectFirstEvent)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return;
            }

            _suppressUi = true;
            try
            {
                if (_controlTypeCombo.Items.Contains(type))
                {
                    _controlTypeCombo.SelectedItem = type;
                }
            }
            finally
            {
                _suppressUi = false;
            }

            CommitLiteral("ControlType", type);
            RefreshEventNames(selectFirstIfMissing: selectFirstEvent);
        }

        private void RefreshEventNames(bool selectFirstIfMissing)
        {
            string type = GetSelectedControlType();
            string previous = _eventNameCombo.SelectedItem == null
                ? string.Empty
                : Convert.ToString(_eventNameCombo.SelectedItem);

            _suppressUi = true;
            try
            {
                _eventNameCombo.Items.Clear();
                if (!string.IsNullOrEmpty(type))
                {
                    foreach (string eventName in FormEventCatalog.GetEventsForControlType(type))
                    {
                        _eventNameCombo.Items.Add(eventName);
                    }
                }

                if (!string.IsNullOrWhiteSpace(previous) && _eventNameCombo.Items.Contains(previous))
                {
                    _eventNameCombo.SelectedItem = previous;
                }
                else if (selectFirstIfMissing && _eventNameCombo.Items.Count > 0)
                {
                    _eventNameCombo.SelectedIndex = 0;
                    previous = Convert.ToString(_eventNameCombo.SelectedItem);
                }
                else
                {
                    _eventNameCombo.SelectedIndex = -1;
                    previous = string.Empty;
                }
            }
            finally
            {
                _suppressUi = false;
            }

            if (!string.IsNullOrWhiteSpace(previous))
            {
                CommitLiteral("EventName", previous);
            }
            else
            {
                DesignTimeFormPath.SetOptionalLiteralString(ModelItem, "EventName", null);
            }
        }

        private string GetSelectedControlType()
        {
            if (_controlTypeCombo.SelectedItem != null)
            {
                return Convert.ToString(_controlTypeCombo.SelectedItem);
            }

            if (ModelItem != null
                && DesignTimeFormPath.TryGetLiteralString(ModelItem.Properties["ControlType"].Value, out string stored)
                && !string.IsNullOrWhiteSpace(stored))
            {
                return stored.Trim();
            }

            return null;
        }

        private void SyncCombosFromModel()
        {
            if (ModelItem == null)
            {
                return;
            }

            string persistControlType = null;
            string persistEventName = null;
            string persistUiBehavior = null;

            _suppressUi = true;
            try
            {
                string controlId = null;
                string controlType = null;
                string eventName = null;
                string uiBehavior = null;

                DesignTimeFormPath.TryGetLiteralString(ModelItem.Properties["ControlId"].Value, out controlId);
                DesignTimeFormPath.TryGetLiteralString(ModelItem.Properties["ControlType"].Value, out controlType);
                DesignTimeFormPath.TryGetLiteralString(ModelItem.Properties["EventName"].Value, out eventName);
                DesignTimeFormPath.TryGetLiteralString(ModelItem.Properties["UiBehavior"].Value, out uiBehavior);

                SetControlIdCombo(controlId);

                if (!string.IsNullOrWhiteSpace(controlType) && _controlTypeCombo.Items.Contains(controlType))
                {
                    _controlTypeCombo.SelectedItem = controlType;
                }
                else
                {
                    FormEventCatalog.ControlRef match = FindControlById(controlId);
                    if (match != null && _controlTypeCombo.Items.Contains(match.Type))
                    {
                        _controlTypeCombo.SelectedItem = match.Type;
                        controlType = match.Type;
                        persistControlType = match.Type;
                    }
                    else
                    {
                        _controlTypeCombo.SelectedIndex = -1;
                    }
                }

                _eventNameCombo.Items.Clear();
                if (!string.IsNullOrWhiteSpace(controlType))
                {
                    foreach (string name in FormEventCatalog.GetEventsForControlType(controlType))
                    {
                        _eventNameCombo.Items.Add(name);
                    }
                }

                if (!string.IsNullOrWhiteSpace(eventName) && _eventNameCombo.Items.Contains(eventName))
                {
                    _eventNameCombo.SelectedItem = eventName;
                }
                else if (_eventNameCombo.Items.Count > 0 && !string.IsNullOrWhiteSpace(controlType))
                {
                    // Type known but event empty/invalid — default to first.
                    _eventNameCombo.SelectedIndex = 0;
                    persistEventName = Convert.ToString(_eventNameCombo.SelectedItem);
                }
                else
                {
                    _eventNameCombo.SelectedIndex = -1;
                }

                if (!string.IsNullOrWhiteSpace(uiBehavior) && _uiBehaviorCombo.Items.Contains(uiBehavior))
                {
                    _uiBehaviorCombo.SelectedItem = uiBehavior;
                }
                else
                {
                    _uiBehaviorCombo.SelectedItem = "NoLock";
                    persistUiBehavior = "NoLock";
                }
            }
            finally
            {
                _suppressUi = false;
            }

            if (persistControlType != null)
            {
                CommitLiteral("ControlType", persistControlType);
            }

            if (persistEventName != null)
            {
                CommitLiteral("EventName", persistEventName);
            }

            if (persistUiBehavior != null)
            {
                CommitLiteral("UiBehavior", persistUiBehavior);
            }
        }

        private void EnsureUiBehaviorDefault()
        {
            if (ModelItem == null)
            {
                return;
            }

            if (!DesignTimeFormPath.TryGetLiteralString(ModelItem.Properties["UiBehavior"].Value, out string uiBehavior)
                || string.IsNullOrWhiteSpace(uiBehavior))
            {
                CommitLiteral("UiBehavior", "NoLock");
                _suppressUi = true;
                try
                {
                    _uiBehaviorCombo.SelectedItem = "NoLock";
                }
                finally
                {
                    _suppressUi = false;
                }
            }
        }

        private void CommitLiteral(string propertyName, string value)
        {
            if (_suppressUi || ModelItem == null)
            {
                return;
            }

            DesignTimeFormPath.SetLiteralString(ModelItem, propertyName, value ?? string.Empty);
        }

        private string GetControlIdText()
        {
            if (_controlIdCombo.SelectedItem is FormEventCatalog.ControlRef controlRef)
            {
                return controlRef.Id ?? string.Empty;
            }

            return _controlIdCombo.Text == null ? string.Empty : _controlIdCombo.Text.Trim();
        }

        private void SetControlIdCombo(string controlId)
        {
            if (string.IsNullOrEmpty(controlId))
            {
                _controlIdCombo.Text = string.Empty;
                _controlIdCombo.SelectedIndex = -1;
                return;
            }

            foreach (object item in _controlIdCombo.Items)
            {
                if (item is FormEventCatalog.ControlRef controlRef
                    && string.Equals(controlRef.Id, controlId, StringComparison.OrdinalIgnoreCase))
                {
                    _controlIdCombo.SelectedItem = item;
                    return;
                }
            }

            _controlIdCombo.SelectedIndex = -1;
            _controlIdCombo.Text = controlId;
        }

        private FormEventCatalog.ControlRef FindControlById(string controlId)
        {
            if (string.IsNullOrWhiteSpace(controlId) || _controls == null)
            {
                return null;
            }

            foreach (FormEventCatalog.ControlRef control in _controls)
            {
                if (string.Equals(control.Id, controlId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return control;
                }
            }

            return null;
        }

        private static string NormalizeControlIdInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = value.Trim();
            int open = text.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && text.EndsWith(")", StringComparison.Ordinal))
            {
                return text.Substring(0, open).Trim();
            }

            return text;
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        private static ComboBox CreateEditableCombo()
        {
            return new ComboBox
            {
                IsEditable = true,
                IsTextSearchEnabled = false,
                MinWidth = 280,
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static ComboBox CreateFixedCombo()
        {
            return new ComboBox
            {
                IsEditable = false,
                IsTextSearchEnabled = false,
                MinWidth = 280,
                Margin = new Thickness(0, 0, 0, 6)
            };
        }
    }
}
