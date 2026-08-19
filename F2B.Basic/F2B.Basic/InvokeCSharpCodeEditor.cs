using System;
using System.Activities;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    /// <summary>
    /// Shared Program.cs editor used by the canvas Edit Code button and the property-grid "..." button.
    /// </summary>
    internal static class InvokeCSharpCodeEditor
    {
        public static bool Show(Window owner, System.Activities.Presentation.Model.ModelItem modelItem)
        {
            if (modelItem == null)
            {
                return false;
            }

            string current = modelItem.GetValue<string>("Code");
            if (string.IsNullOrWhiteSpace(current))
            {
                current = InvokeCSharpCodeActivity.DefaultCode;
            }

            var editor = new TextBox
            {
                Text = current,
                AcceptsReturn = true,
                AcceptsTab = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                MinLines = 18
            };

            var hint = new TextBlock
            {
                Text = BuildHint(modelItem),
                FontSize = 11,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var ok = new Button
            {
                Content = "OK",
                Width = 88,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancel = new Button
            {
                Content = "Cancel",
                Width = 88,
                Height = 26,
                IsCancel = true
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var host = new DockPanel { LastChildFill = true, Margin = new Thickness(10) };
            DockPanel.SetDock(hint, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            host.Children.Add(hint);
            host.Children.Add(buttons);
            host.Children.Add(editor);

            var window = new Window
            {
                Title = "Invoke C# Code",
                Content = host,
                Width = 780,
                Height = 560,
                MinWidth = 520,
                MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                ShowInTaskbar = false,
                Background = Brushes.White
            };

            if (owner != null)
            {
                window.Owner = owner;
            }

            ok.Click += (s, e) => { window.DialogResult = true; };

            if (window.ShowDialog() != true || editor.Text == current)
            {
                return false;
            }

            modelItem.SetValueInArg("Code", editor.Text);
            return true;
        }

        private static string BuildHint(System.Activities.Presentation.Model.ModelItem modelItem)
        {
            var lines = new List<string>
            {
                "Write a full Program.cs. Entry: Program.Main(Args args) or Main(). using is optional. Console.WriteLine goes to Output."
            };

            Dictionary<string, Argument> arguments = null;
            try
            {
                if (modelItem != null && modelItem.Properties["Arguments"] != null)
                {
                    arguments = modelItem.Properties["Arguments"].ComputedValue as Dictionary<string, Argument>;
                }
            }
            catch
            {
            }

            IList<string> properties = InvokeCSharpCodeHost.DescribeArgsProperties(arguments);
            if (properties != null && properties.Count > 0)
            {
                lines.Add("Args: " + string.Join("; ", properties));
            }

            return string.Join(" ", lines.ToArray());
        }
    }
}
