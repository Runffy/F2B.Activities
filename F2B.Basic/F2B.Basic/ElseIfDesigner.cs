using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    public sealed class ElseIfDesigner : ActivityDesigner
    {
        private const string LabelColumnGroup = "ElseIfConditionLabel";
        private const double LabelColumnWidth = 72;
        private const double ConditionEditorWidth = 200;

        private readonly StackPanel _root;
        private readonly StackPanel _elseIfsPanel;
        private readonly FrameworkElement _elseSection;
        private readonly Button _showHideElseButton;
        private readonly Border _ifConditionBorder;
        private readonly List<Border> _elseIfConditionBorders = new List<Border>();
        private ModelItemCollection _elseIfs;
        private bool _elseVisible;
        private bool _rebuilding;

        public ElseIfDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                MinWidth = 320,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            _root = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetIsSharedSizeScope(_root, true);

            _root.Children.Add(CreateSectionTitle("If", isFirst: true));
            _root.Children.Add(CreateIfConditionEditor(out _ifConditionBorder));
            _root.Children.Add(CreateBodyPresenter("ModelItem.Then"));

            _elseIfsPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            _root.Children.Add(_elseIfsPanel);

            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var addButton = new Button
            {
                Content = "Add ElseIf",
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 96
            };
            addButton.Click += OnAddElseIfClicked;
            toolbar.Children.Add(addButton);

            // Use Button (not TextBlock): TextBlock often needs a focus click before MouseUp fires.
            _showHideElseButton = new Button
            {
                Content = "Show Else",
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, 2, 0, 2),
                Margin = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.DarkBlue,
                FontWeight = FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _showHideElseButton.Template = CreateLinkButtonTemplate();
            _showHideElseButton.Click += OnShowHideElseClicked;
            toolbar.Children.Add(_showHideElseButton);
            _root.Children.Add(toolbar);

            _elseSection = CreateElseSection();
            _elseSection.Visibility = Visibility.Collapsed;
            _root.Children.Add(_elseSection);

            border.Child = _root;
            Content = border;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private FrameworkElement CreateElseSection()
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            panel.Children.Add(CreateSectionTitle("Else", isFirst: false));
            panel.Children.Add(CreateBodyPresenter("ModelItem.Else"));
            return panel;
        }

        private static TextBlock CreateSectionTitle(string title, bool isFirst)
        {
            return new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, isFirst ? 0 : 12, 0, 4)
            };
        }

        private FrameworkElement CreateIfConditionEditor(out Border editorBorder)
        {
            return CreateConditionRow("ModelItem.Condition", null, out editorBorder);
        }

        private FrameworkElement CreateBranchConditionEditor(ModelItem branchItem, out Border editorBorder)
        {
            return CreateConditionRow(null, branchItem, out editorBorder);
        }

        private FrameworkElement CreateConditionRow(
            string bindToModelItemPath,
            ModelItem branchItem,
            out Border editorBorder)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = LabelColumnGroup,
                MinWidth = LabelColumnWidth
            });
            // Fixed-width condition editor — do not grow with expression text length.
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Condition",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var expressionTextBox = new ExpressionTextBox
            {
                HintText = "Boolean expression",
                ExpressionType = typeof(bool),
                MinLines = 1,
                MaxLines = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = ConditionEditorWidth,
                MinWidth = ConditionEditorWidth,
                MaxWidth = ConditionEditorWidth
            };

            BindingOperations.SetBinding(
                expressionTextBox,
                ExpressionTextBox.OwnerActivityProperty,
                new Binding("ModelItem") { Source = this });

            if (branchItem != null)
            {
                BindingOperations.SetBinding(
                    expressionTextBox,
                    ExpressionTextBox.ExpressionProperty,
                    new Binding("Condition")
                    {
                        Source = branchItem,
                        Mode = BindingMode.TwoWay,
                        Converter = new ArgumentToExpressionConverter(),
                        ConverterParameter = "In"
                    });
            }
            else
            {
                BindingOperations.SetBinding(
                    expressionTextBox,
                    ExpressionTextBox.ExpressionProperty,
                    new Binding(bindToModelItemPath)
                    {
                        Source = this,
                        Mode = BindingMode.TwoWay,
                        Converter = new ArgumentToExpressionConverter(),
                        ConverterParameter = "In"
                    });
            }

            editorBorder = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionTextBox,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = ConditionEditorWidth,
                MaxWidth = ConditionEditorWidth,
                Tag = branchItem
            };
            Grid.SetColumn(editorBorder, 1);
            row.Children.Add(editorBorder);

            expressionTextBox.LostKeyboardFocus += (s, e) => RefreshAllConditionBorders();
            expressionTextBox.LostFocus += (s, e) => RefreshAllConditionBorders();

            return row;
        }

        private FrameworkElement CreateBodyPresenter(string bindingPath)
        {
            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop activity here",
                MinWidth = 240,
                MinHeight = 40,
                VerticalAlignment = VerticalAlignment.Top
            };
            BindingOperations.SetBinding(
                presenter,
                WorkflowItemPresenter.ItemProperty,
                new Binding(bindingPath)
                {
                    Source = this,
                    Mode = BindingMode.TwoWay
                });
            return ActivityBodyExpandHelper.WrapExpandingBody(this, presenter);
        }

        private FrameworkElement CreateBranchBodyPresenter(ModelItem branchItem)
        {
            var presenter = new WorkflowItemPresenter
            {
                HintText = "Drop activity here",
                MinWidth = 240,
                MinHeight = 40,
                VerticalAlignment = VerticalAlignment.Top
            };
            BindingOperations.SetBinding(
                presenter,
                WorkflowItemPresenter.ItemProperty,
                new Binding("Body")
                {
                    Source = branchItem,
                    Mode = BindingMode.TwoWay
                });
            return ActivityBodyExpandHelper.WrapExpandingBody(this, presenter);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            EnsureElseIfsCollection();
            RebuildElseIfRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshAllConditionBorders();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachElseIfsCollection();
            if (ModelItem != null)
            {
                ModelItem.PropertyChanged -= OnModelItemPropertyChanged;
            }
        }

        private void EnsureElseIfsCollection()
        {
            DetachElseIfsCollection();
            _elseIfs = ModelItem.Properties["ElseIfs"]?.Collection;
            if (_elseIfs != null)
            {
                _elseIfs.CollectionChanged += OnElseIfsCollectionChanged;
            }
        }

        private void DetachElseIfsCollection()
        {
            if (_elseIfs != null)
            {
                _elseIfs.CollectionChanged -= OnElseIfsCollectionChanged;
                _elseIfs = null;
            }
        }

        private void OnElseIfsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_rebuilding)
            {
                return;
            }

            RebuildElseIfRows();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshAllConditionBorders), DispatcherPriority.Background);
        }

        private void RebuildElseIfRows()
        {
            if (_elseIfsPanel == null)
            {
                return;
            }

            if (_elseIfs == null && ModelItem != null)
            {
                EnsureElseIfsCollection();
            }

            _rebuilding = true;
            try
            {
                _elseIfsPanel.Children.Clear();
                _elseIfConditionBorders.Clear();
                if (_elseIfs == null)
                {
                    return;
                }

                foreach (ModelItem branchItem in _elseIfs)
                {
                    _elseIfsPanel.Children.Add(CreateElseIfRow(branchItem));
                }
            }
            finally
            {
                _rebuilding = false;
            }

            RefreshAllConditionBorders();
        }

        private FrameworkElement CreateElseIfRow(ModelItem branchItem)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            var header = new Grid
            {
                Margin = new Thickness(0, 12, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            header.Children.Add(new TextBlock
            {
                Text = "Else If",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var deleteButton = new Button
            {
                Content = "X",
                Width = 26,
                Height = 22,
                Padding = new Thickness(0),
                Tag = branchItem,
                ToolTip = "Delete Else If branch",
                VerticalAlignment = VerticalAlignment.Center
            };
            deleteButton.Click += OnDeleteElseIfClicked;
            Grid.SetColumn(deleteButton, 1);
            header.Children.Add(deleteButton);
            panel.Children.Add(header);

            Border conditionBorder;
            panel.Children.Add(CreateBranchConditionEditor(branchItem, out conditionBorder));
            _elseIfConditionBorders.Add(conditionBorder);

            panel.Children.Add(CreateBranchBodyPresenter(branchItem));
            return panel;
        }

        private void OnAddElseIfClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            if (_elseIfs == null)
            {
                EnsureElseIfsCollection();
            }

            if (_elseIfs == null)
            {
                return;
            }

            using (ModelEditingScope scope = ModelItem.BeginEdit("Add Else If"))
            {
                _elseIfs.Add(new ElseIfBranch
                {
                    Body = new Sequence()
                });
                scope.Complete();
            }
        }

        private void OnDeleteElseIfClicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var branchItem = button?.Tag as ModelItem;
            if (branchItem == null || ModelItem == null || _elseIfs == null)
            {
                return;
            }

            using (ModelEditingScope scope = ModelItem.BeginEdit("Delete Else If"))
            {
                _elseIfs.Remove(branchItem);
                scope.Complete();
            }
        }

        private void OnShowHideElseClicked(object sender, RoutedEventArgs e)
        {
            _elseVisible = !_elseVisible;
            _elseSection.Visibility = _elseVisible ? Visibility.Visible : Visibility.Collapsed;
            _showHideElseButton.Content = _elseVisible ? "Hide Else" : "Show Else";
        }

        private static ControlTemplate CreateLinkButtonTemplate()
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding("Content")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            text.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            text.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            var template = new ControlTemplate(typeof(Button));
            template.VisualTree = text;
            return template;
        }

        private void RefreshAllConditionBorders()
        {
            SetRequiredBorder(_ifConditionBorder, IsArgumentFilled(ModelItem, "Condition"));

            foreach (Border border in _elseIfConditionBorders)
            {
                var branchItem = border.Tag as ModelItem;
                SetRequiredBorder(border, IsArgumentFilled(branchItem, "Condition"));
            }
        }

        private static bool IsArgumentFilled(ModelItem owner, string propertyName)
        {
            var property = owner?.Properties[propertyName];
            if (property == null || !property.IsSet || property.Value == null)
            {
                return false;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return false;
            }

            return expressionProperty.Value != null || expressionProperty.ComputedValue != null;
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
