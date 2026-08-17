using System;
using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    /// <summary>
    /// Lets nested activity designers grow the host scope width instead of being clipped
    /// to a fixed presenter width.
    /// </summary>
    internal static class ActivityBodyExpandHelper
    {
        public static FrameworkElement WrapExpandingBody(ActivityDesigner owner, WorkflowItemPresenter presenter)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            var host = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = Math.Max(40, presenter.MinHeight)
            };

            presenter.HorizontalAlignment = HorizontalAlignment.Center;
            presenter.HorizontalContentAlignment = HorizontalAlignment.Center;

            var state = new ExpandState(owner, host, presenter);
            Action apply = state.Apply;

            host.SizeChanged += (s, e) => apply();
            presenter.Loaded += (s, e) =>
            {
                apply();
                presenter.Dispatcher.BeginInvoke(apply, DispatcherPriority.Loaded);
                presenter.Dispatcher.BeginInvoke(apply, DispatcherPriority.ContextIdle);
            };

            DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty(
                WorkflowItemPresenter.ItemProperty,
                typeof(WorkflowItemPresenter));
            if (descriptor != null)
            {
                descriptor.AddValueChanged(presenter, (s, e) =>
                {
                    presenter.Dispatcher.BeginInvoke(apply, DispatcherPriority.Loaded);
                    presenter.Dispatcher.BeginInvoke(apply, DispatcherPriority.ContextIdle);
                });
            }

            host.Children.Add(presenter);
            return host;
        }

        private sealed class ExpandState
        {
            private readonly ActivityDesigner _owner;
            private readonly FrameworkElement _host;
            private readonly WorkflowItemPresenter _presenter;
            private bool _expanding;

            public ExpandState(ActivityDesigner owner, FrameworkElement host, WorkflowItemPresenter presenter)
            {
                _owner = owner;
                _host = host;
                _presenter = presenter;
            }

            public void Apply()
            {
                if (_owner == null || _host == null || _presenter == null || !_owner.IsLoaded || _expanding)
                {
                    return;
                }

                _expanding = true;
                try
                {
                    ActivityDesigner nestedDesigner = FindDescendantActivityDesigner(_presenter);
                    double needed = 0;

                    if (nestedDesigner != null)
                    {
                        nestedDesigner.HorizontalAlignment = HorizontalAlignment.Center;
                        nestedDesigner.ClearValue(FrameworkElement.WidthProperty);
                        nestedDesigner.ClearValue(FrameworkElement.MaxWidthProperty);

                        foreach (WorkflowItemsPresenter itemsPresenter in FindDescendantsOfType<WorkflowItemsPresenter>(nestedDesigner))
                        {
                            itemsPresenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                            itemsPresenter.ClearValue(FrameworkElement.WidthProperty);
                            itemsPresenter.ClearValue(FrameworkElement.MaxWidthProperty);
                        }

                        nestedDesigner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        needed = Math.Max(needed, nestedDesigner.DesiredSize.Width);
                    }

                    _presenter.ClearValue(FrameworkElement.WidthProperty);
                    _presenter.ClearValue(FrameworkElement.MaxWidthProperty);
                    _presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    needed = Math.Max(needed, _presenter.DesiredSize.Width);

                    if (needed <= 1)
                    {
                        return;
                    }

                    if (_host.MinWidth + 0.5 < needed)
                    {
                        _host.MinWidth = needed;
                    }

                    var border = _owner.Content as Border;
                    if (border != null)
                    {
                        double borderNeeded = needed + border.Padding.Left + border.Padding.Right;
                        if (border.MinWidth + 0.5 < borderNeeded)
                        {
                            border.MinWidth = borderNeeded;
                        }
                    }

                    _owner.ClearValue(FrameworkElement.MaxWidthProperty);
                    double designerNeeded = needed + 24;
                    if (_owner.MinWidth + 0.5 < designerNeeded)
                    {
                        _owner.MinWidth = designerNeeded;
                    }
                }
                finally
                {
                    _expanding = false;
                }
            }
        }

        private static ActivityDesigner FindDescendantActivityDesigner(DependencyObject root)
        {
            if (root == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                var designer = child as ActivityDesigner;
                if (designer != null)
                {
                    return designer;
                }

                designer = FindDescendantActivityDesigner(child);
                if (designer != null)
                {
                    return designer;
                }
            }

            return null;
        }

        private static IEnumerable<T> FindDescendantsOfType<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T nested in FindDescendantsOfType<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }
}
