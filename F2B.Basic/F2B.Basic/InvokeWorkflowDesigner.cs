using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using F2B.OpenRpa.Design;
using OpenRPA;
using OpenRPA.Interfaces;
using OpenRPA.Interfaces.entity;

namespace F2B.Basic
{
    public sealed class InvokeWorkflowDesigner : ActivityDesigner, INotifyPropertyChanged
    {
        private const double DesignerContentWidth = 340;

        private readonly ComboBox _projectCombo;
        private readonly ComboBox _workflowCombo;
        private readonly Border _workflowEditorBorder;
        private readonly TextBlock _workflowSummary;
        private bool _suppressSelectionHandlers;
        private string _selectedProjectName;

        public InvokeWorkflowDesigner()
        {
            Projects = new ObservableCollection<string>();
            Workflows = new ObservableCollection<IWorkflow>();

            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Width = DesignerContentWidth,
                MaxWidth = DesignerContentWidth,
                MinWidth = DesignerContentWidth,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var panel = new StackPanel
            {
                Width = DesignerContentWidth - 14,
                MaxWidth = DesignerContentWidth - 14
            };

            panel.Children.Add(CreateLabel("Project"));
            _projectCombo = new ComboBox
            {
                Width = DesignerContentWidth - 14,
                MaxWidth = DesignerContentWidth - 14,
                Margin = new Thickness(0, 0, 0, 6),
                ItemsSource = Projects
            };
            _projectCombo.SelectionChanged += OnProjectSelectionChanged;
            panel.Children.Add(_projectCombo);

            panel.Children.Add(CreateLabel("Workflow"));
            _workflowCombo = new ComboBox
            {
                Width = DesignerContentWidth - 14,
                MaxWidth = DesignerContentWidth - 14,
                Margin = new Thickness(0, 0, 0, 6),
                DisplayMemberPath = "name",
                ItemsSource = Workflows
            };
            _workflowCombo.SelectionChanged += OnWorkflowSelectionChanged;
            panel.Children.Add(_workflowCombo);

            _workflowSummary = new TextBlock
            {
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
                Text = "Workflow: (not selected)"
            };
            _workflowEditorBorder = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = _workflowSummary
            };
            panel.Children.Add(_workflowEditorBorder);

            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
                "Log Input",
                "ModelItem.LogInputArguments",
                typeof(bool),
                "false",
                out _,
                out _,
                editorWidth: 180));

            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
                "Log Output",
                "ModelItem.LogOutputArguments",
                typeof(bool),
                "false",
                out _,
                out _,
                editorWidth: 180));

            var mapButton = new Button
            {
                Content = "Map Arguments",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            mapButton.Click += OnMapArgumentsClicked;
            panel.Children.Add(mapButton);

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += OnLoaded;
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<string> Projects { get; private set; }

        public ObservableCollection<IWorkflow> Workflows { get; private set; }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ReloadProjectWorkflowLists();
                RefreshRequiredBorder();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private void ReloadProjectWorkflowLists()
        {
            if (RobotInstance.instance == null || RobotInstance.instance.Projects == null)
            {
                return;
            }

            _suppressSelectionHandlers = true;
            try
            {
                Projects.Clear();
                foreach (string projectName in RobotInstance.instance.Projects
                    .Select(p => p.name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    Projects.Add(projectName);
                }

                string selectedWorkflowKey = GetSelectedWorkflowKey();
                string projectNameFromKey;
                string workflowNameFromKey;
                TrySplitProjectAndName(selectedWorkflowKey, out projectNameFromKey, out workflowNameFromKey);

                if (!string.IsNullOrWhiteSpace(projectNameFromKey)
                    && Projects.Contains(projectNameFromKey))
                {
                    _selectedProjectName = projectNameFromKey;
                    _projectCombo.SelectedItem = projectNameFromKey;
                }
                else if (Projects.Count > 0 && string.IsNullOrWhiteSpace(selectedWorkflowKey))
                {
                    _selectedProjectName = null;
                    _projectCombo.SelectedItem = null;
                }

                RebuildWorkflowCombo(preserveWorkflowName: workflowNameFromKey);

                if (!string.IsNullOrWhiteSpace(workflowNameFromKey))
                {
                    IWorkflow match = Workflows.FirstOrDefault(w =>
                        string.Equals(w.name, workflowNameFromKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(w.ProjectAndName, selectedWorkflowKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(w.RelativeFilename, selectedWorkflowKey, StringComparison.OrdinalIgnoreCase));
                    _workflowCombo.SelectedItem = match;
                    if (match != null
                        && !string.IsNullOrWhiteSpace(match.ProjectAndName)
                        && !string.Equals(match.ProjectAndName, selectedWorkflowKey, StringComparison.Ordinal))
                    {
                        SetWorkflowKey(match.ProjectAndName);
                    }
                }
            }
            finally
            {
                _suppressSelectionHandlers = false;
            }
        }

        private void RebuildWorkflowCombo(string preserveWorkflowName)
        {
            Workflows.Clear();
            if (RobotInstance.instance == null)
            {
                return;
            }

            IEnumerable<IWorkflow> source = RobotInstance.instance.Workflows ?? Enumerable.Empty<IWorkflow>();
            OpenRPA.Views.WFDesigner designer = RobotInstance.instance.Window?.Designer as OpenRPA.Views.WFDesigner;

            foreach (IWorkflow workflow in source
                .Where(w => w != null)
                .Where(w => MatchesSelectedProject(w))
                .Where(w => designer == null || designer.Workflow == null || designer.Workflow._id != w._id || w._id == null)
                .OrderBy(w => w.name, StringComparer.OrdinalIgnoreCase))
            {
                Workflows.Add(workflow);
            }

            if (!string.IsNullOrWhiteSpace(preserveWorkflowName))
            {
                IWorkflow keep = Workflows.FirstOrDefault(w =>
                    string.Equals(w.name, preserveWorkflowName, StringComparison.OrdinalIgnoreCase));
                _workflowCombo.SelectedItem = keep;
            }
            else
            {
                _workflowCombo.SelectedItem = null;
            }
        }

        private bool MatchesSelectedProject(IWorkflow workflow)
        {
            if (string.IsNullOrWhiteSpace(_selectedProjectName))
            {
                return false;
            }

            string projectAndName = workflow.ProjectAndName;
            if (!string.IsNullOrWhiteSpace(projectAndName))
            {
                string project;
                string name;
                if (TrySplitProjectAndName(projectAndName, out project, out name)
                    && string.Equals(project, _selectedProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            try
            {
                var projectMethod = workflow.GetType().GetMethod("Project", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                object projectObj = projectMethod?.Invoke(workflow, null);
                string projectName = projectObj == null
                    ? null
                    : (projectObj as IBase)?.name
                      ?? projectObj.GetType().GetProperty("name")?.GetValue(projectObj) as string;
                return string.Equals(projectName, _selectedProjectName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void OnProjectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionHandlers)
            {
                return;
            }

            string projectName = _projectCombo.SelectedItem as string;
            if (string.Equals(projectName, _selectedProjectName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedProjectName = projectName;

            // Same as single-dropdown UX when the selected identity becomes invalid: clear workflow key until a new workflow is chosen.
            _suppressSelectionHandlers = true;
            try
            {
                RebuildWorkflowCombo(preserveWorkflowName: null);
                SetWorkflowKey(null);
            }
            finally
            {
                _suppressSelectionHandlers = false;
            }

            RefreshRequiredBorder();
            NotifyPropertyChanged(nameof(Workflows));
        }

        private void OnWorkflowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionHandlers)
            {
                return;
            }

            var workflow = _workflowCombo.SelectedItem as IWorkflow;
            if (workflow == null)
            {
                SetWorkflowKey(null);
            }
            else
            {
                SetWorkflowKey(workflow.ProjectAndName ?? workflow.RelativeFilename);
            }

            RefreshRequiredBorder();
        }

        private void OnMapArgumentsClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            string workflowId = GetSelectedWorkflowKey();
            ModelItemDictionary dictionary = ModelItem.Properties["Arguments"].Dictionary;
            if (!string.IsNullOrEmpty(workflowId))
            {
                IWorkflow workflow = RobotInstance.instance.GetWorkflowByIDOrRelativeFilename(workflowId);
                if (workflow != null)
                {
                    try
                    {
                        workflow.ParseParameters();
                    }
                    catch
                    {
                        workflow = null;
                    }
                }

                if (workflow != null)
                {
                    foreach (workflowparameter p in workflow.Parameters)
                    {
                        bool exists = false;
                        foreach (ModelItem key in dictionary.Keys)
                        {
                            if (key.ToString() == p.name)
                            {
                                exists = true;
                            }

                            if (OpenRPA.Interfaces.Extensions.GetValue<string>(key, "AnnotationText") == p.name)
                            {
                                exists = true;
                            }

                            if (OpenRPA.Interfaces.Extensions.GetValue<string>(key, "Name") == p.name)
                            {
                                exists = true;
                            }
                        }

                        if (!exists)
                        {
                            Type t = OpenRPA.Interfaces.Extensions.FindType(p.type);
                            if (p.type == "System.Data.DataTable")
                            {
                                t = typeof(System.Data.DataTable);
                            }

                            if (t == null)
                            {
                                throw new ArgumentException("Failed resolving type '" + p.type + "'");
                            }

                            Argument a = null;
                            if (p.direction == workflowparameterdirection.@in)
                            {
                                a = Argument.Create(t, ArgumentDirection.In);
                            }

                            if (p.direction == workflowparameterdirection.inout)
                            {
                                a = Argument.Create(t, ArgumentDirection.InOut);
                            }

                            if (p.direction == workflowparameterdirection.@out)
                            {
                                a = Argument.Create(t, ArgumentDirection.Out);
                            }

                            dictionary.Add(p.name, a);
                        }
                    }

                    foreach (var a in dictionary.ToList())
                    {
                        bool exists = workflow.Parameters.Any(x => x.name == a.Key.ToString());
                        if (!exists)
                        {
                            dictionary.Remove(a.Key);
                        }
                    }
                }
            }

            var options = new System.Activities.Presentation.DynamicArgumentDesignerOptions
            {
                Title = OpenRPA.Interfaces.Extensions.GetValue<string>(ModelItem, "DisplayName") ?? "Invoke Workflow"
            };
            using (ModelEditingScope modelEditingScope = dictionary.BeginEdit())
            {
                if (System.Activities.Presentation.DynamicArgumentDialog.ShowDialog(
                    ModelItem, dictionary, Context, ModelItem.View, options))
                {
                    modelEditingScope.Complete();
                }
                else
                {
                    modelEditingScope.Revert();
                }
            }
        }

        private string GetSelectedWorkflowKey()
        {
            if (ModelItem == null)
            {
                return null;
            }

            try
            {
                return OpenRPA.Interfaces.Extensions.GetValue<string>(ModelItem, "Workflow");
            }
            catch
            {
                return null;
            }
        }

        private void SetWorkflowKey(string value)
        {
            if (ModelItem == null)
            {
                return;
            }

            using (ModelEditingScope scope = ModelItem.BeginEdit("Set Workflow"))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    ModelItem.Properties["Workflow"].ClearValue();
                }
                else
                {
                    OpenRPA.Interfaces.Extensions.SetValueInArg(ModelItem, "Workflow", value.Replace("\\", "/"));
                }

                scope.Complete();
            }
        }

        private static bool TrySplitProjectAndName(string projectAndName, out string projectName, out string workflowName)
        {
            projectName = null;
            workflowName = null;
            if (string.IsNullOrWhiteSpace(projectAndName))
            {
                return false;
            }

            string normalized = projectAndName.Replace("\\", "/");
            int slash = normalized.IndexOf('/');
            if (slash <= 0 || slash >= normalized.Length - 1)
            {
                workflowName = normalized;
                return false;
            }

            projectName = normalized.Substring(0, slash);
            workflowName = normalized.Substring(slash + 1);
            return true;
        }

        private void RefreshRequiredBorder()
        {
            string key = GetSelectedWorkflowKey();
            bool filled = !string.IsNullOrWhiteSpace(key);
            if (_workflowSummary != null)
            {
                _workflowSummary.Text = filled ? ("Workflow: " + key) : "Workflow: (not selected)";
            }

            if (_workflowEditorBorder == null)
            {
                return;
            }

            if (filled)
            {
                _workflowEditorBorder.BorderBrush = Brushes.Transparent;
                _workflowEditorBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                _workflowEditorBorder.BorderBrush = Brushes.Red;
                _workflowEditorBorder.BorderThickness = new Thickness(1);
            }
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
