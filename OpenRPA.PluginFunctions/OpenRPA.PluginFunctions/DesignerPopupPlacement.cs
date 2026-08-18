using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Places designer command popups just under the ribbon, horizontally centered
    /// in the current workflow designer pane.
    /// </summary>
    internal static class DesignerPopupPlacement
    {
        public static void ApplyUpperThird(Popup popup, Window window)
        {
            if (popup == null || window == null)
            {
                return;
            }

            popup.PlacementTarget = window;
            popup.Placement = PlacementMode.Custom;
            popup.HorizontalOffset = 0;
            popup.VerticalOffset = 0;
            popup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
                PlaceOverDesigner(window, popupSize, targetSize);
        }

        private static CustomPopupPlacement[] PlaceOverDesigner(
            Window window,
            Size popupSize,
            Size targetSize)
        {
            double x = ResolveDesignerCenteredX(window, popupSize, targetSize);
            double y = ResolveRibbonBottomY(window);
            if (y < 0)
            {
                y = 0;
            }

            if (y + popupSize.Height > targetSize.Height && targetSize.Height > popupSize.Height)
            {
                y = Math.Max(0, targetSize.Height - popupSize.Height - 8);
            }

            return new[]
            {
                new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.Horizontal)
            };
        }

        private static double ResolveDesignerCenteredX(Window window, Size popupSize, Size targetSize)
        {
            try
            {
                FrameworkElement host = ResolveDesignerHost();
                if (host != null && host.IsVisible && host.ActualWidth > 0)
                {
                    GeneralTransform transform = host.TransformToAncestor(window);
                    Point topLeft = transform.Transform(new Point(0, 0));
                    double centerX = topLeft.X + (host.ActualWidth / 2.0);
                    double x = centerX - (popupSize.Width / 2.0);

                    if (x < 0)
                    {
                        x = 0;
                    }

                    if (x + popupSize.Width > targetSize.Width)
                    {
                        x = Math.Max(0, targetSize.Width - popupSize.Width);
                    }

                    return x;
                }
            }
            catch
            {
            }

            // Fallback: center of the whole window.
            double fallback = (targetSize.Width - popupSize.Width) / 2.0;
            return fallback < 0 ? 0 : fallback;
        }

        private static FrameworkElement ResolveDesignerHost()
        {
            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer == null)
            {
                return null;
            }

            // WFDesigner is a UserControl hosted in the AvalonDock document (red box).
            var host = designer as FrameworkElement;
            if (host != null && host.ActualWidth > 0)
            {
                return host;
            }

            if (designer.WorkflowDesigner != null)
            {
                return designer.WorkflowDesigner.View as FrameworkElement;
            }

            return null;
        }

        private static double ResolveRibbonBottomY(Window window)
        {
            try
            {
                FrameworkElement ribbon = window.FindName("MainRibbon") as FrameworkElement;
                if (ribbon == null)
                {
                    ribbon = FindDescendantByTypeName(window, "Ribbon");
                }

                if (ribbon != null && ribbon.IsVisible && ribbon.ActualHeight > 0)
                {
                    GeneralTransform transform = ribbon.TransformToAncestor(window);
                    Point bottomLeft = transform.Transform(new Point(0, ribbon.ActualHeight));
                    return Math.Max(0, bottomLeft.Y + 2);
                }
            }
            catch
            {
            }

            return 96;
        }

        private static FrameworkElement FindDescendantByTypeName(DependencyObject root, string typeName)
        {
            if (root == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child == null)
                {
                    continue;
                }

                Type type = child.GetType();
                if (string.Equals(type.Name, typeName, StringComparison.Ordinal)
                    || (type.Name != null && type.Name.EndsWith(typeName, StringComparison.Ordinal)))
                {
                    return child as FrameworkElement;
                }

                FrameworkElement nested = FindDescendantByTypeName(child, typeName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
