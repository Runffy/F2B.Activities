using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using F2B.OpenRpa.Design;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    public sealed class InvokeCSharpCodeDesigner : ActivityDesigner
    {
        private const double DesignerContentWidth = 340;
        private readonly TextBlock _preview;
        private readonly TextBlock _argsHint;

        public InvokeCSharpCodeDesigner()
        {
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

            panel.Children.Add(new TextBlock
            {
                Text = "Standalone C# Program.cs. Entry: Main(Args) or Main().",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 6)
            });

            _preview = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(_preview);

            _argsHint = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(_argsHint);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var editButton = new Button
            {
                Content = "Edit Code",
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 110
            };
            editButton.Click += OnEditCodeClicked;
            buttonRow.Children.Add(editButton);

            var argsButton = new Button
            {
                Content = "Map Arguments",
                Padding = new Thickness(10, 3, 10, 3),
                MinWidth = 110
            };
            argsButton.Click += OnMapArgumentsClicked;
            buttonRow.Children.Add(argsButton);

            panel.Children.Add(buttonRow);
            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += (s, e) => RefreshPreview();
        }

        protected override void OnModelItemChanged(object newItem)
        {
            base.OnModelItemChanged(newItem);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_preview == null)
            {
                return;
            }

            string code = GetCode();
            _preview.Text = SummarizeCode(code);
            _argsHint.Text = SummarizeArguments();
        }

        private string GetCode()
        {
            if (ModelItem == null)
            {
                return null;
            }

            try
            {
                return ModelItem.GetValue<string>("Code");
            }
            catch
            {
                return null;
            }
        }

        private static string SummarizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "(empty — click Edit Code)";
            }

            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("using ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.Length > 48)
                {
                    return line.Substring(0, 48) + "…";
                }

                return line;
            }

            return "Program.cs";
        }

        private string SummarizeArguments()
        {
            Dictionary<string, Argument> arguments = GetArguments();
            IList<string> lines = InvokeCSharpCodeHost.DescribeArgsProperties(arguments);
            if (lines == null || lines.Count == 0)
            {
                return "Args: (none)";
            }

            if (lines.Count == 1)
            {
                return "Args: " + lines[0];
            }

            return "Args: " + lines.Count + " properties (see Map Arguments)";
        }

        private Dictionary<string, Argument> GetArguments()
        {
            if (ModelItem == null)
            {
                return null;
            }

            try
            {
                return ModelItem.Properties["Arguments"] != null
                    ? ModelItem.Properties["Arguments"].ComputedValue as Dictionary<string, Argument>
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private void OnEditCodeClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            Window owner = GenericTools.MainWindow ?? Window.GetWindow(this);
            if (InvokeCSharpCodeEditor.Show(owner, ModelItem))
            {
                RefreshPreview();
            }
        }

        private void OnMapArgumentsClicked(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItemDictionary dictionary = ModelItem.Properties["Arguments"].Dictionary;
            if (dictionary == null)
            {
                return;
            }

            var options = new System.Activities.Presentation.DynamicArgumentDesignerOptions
            {
                Title = OpenRPA.Interfaces.Extensions.GetValue<string>(ModelItem, "DisplayName") ?? "Invoke C# Code"
            };
            using (ModelEditingScope scope = dictionary.BeginEdit())
            {
                if (System.Activities.Presentation.DynamicArgumentDialog.ShowDialog(
                    ModelItem, dictionary, Context, ModelItem.View, options))
                {
                    scope.Complete();
                    RefreshPreview();
                }
                else
                {
                    scope.Revert();
                }
            }
        }
    }
}
