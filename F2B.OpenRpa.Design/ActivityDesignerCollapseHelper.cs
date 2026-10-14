using System;
using System.Activities.Presentation;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Wires OpenRPA/WF activity Expand/Collapse (ShowExpanded) for code-first designers.
    /// Safe to call from designer constructors: never throws; Content is always assigned.
    /// </summary>
    public static class ActivityDesignerCollapseHelper
    {
        public static void Attach(ActivityDesigner designer, UIElement expandedContent, string collapsedHint = null)
        {
            if (designer == null)
            {
                throw new ArgumentNullException(nameof(designer));
            }

            if (expandedContent == null)
            {
                throw new ArgumentNullException(nameof(expandedContent));
            }

            try
            {
                var collapsed = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(collapsedHint) ? "Click to view" : collapsedHint.Trim(),
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
                };

                var host = new ContentControl
                {
                    Focusable = false,
                    Content = expandedContent
                };

                void Sync()
                {
                    try
                    {
                        bool showExpanded = true;
                        try
                        {
                            showExpanded = designer.ShowExpanded;
                        }
                        catch
                        {
                            showExpanded = true;
                        }

                        host.Content = showExpanded ? (object)expandedContent : collapsed;
                    }
                    catch
                    {
                        host.Content = expandedContent;
                    }
                }

                designer.Content = host;

                designer.Loaded += (s, e) => Sync();

                DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty(
                    ActivityDesigner.ExpandStateProperty,
                    typeof(ActivityDesigner));
                if (descriptor != null)
                {
                    descriptor.AddValueChanged(designer, (s, e) => Sync());
                }
            }
            catch
            {
                // Last resort: plain content so OpenRPA can still render the activity view.
                designer.Content = expandedContent;
            }
        }
    }
}
