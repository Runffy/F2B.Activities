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
    /// Lets nested activity designers grow the host scope width just enough to show
    /// children, while keeping a stable base MinWidth when content is narrower.
    /// Nested content is centered inside the container.
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

            double basePresenterMin = presenter.MinWidth > 0 ? presenter.MinWidth : 240;

            var host = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = basePresenterMin,
                MinHeight = Math.Max(40, presenter.MinHeight)
            };

            presenter.HorizontalAlignment = HorizontalAlignment.Center;
            presenter.HorizontalContentAlignment = HorizontalAlignment.Center;
            presenter.MinWidth = basePresenterMin;

            var state = new ExpandState(owner, host, presenter, basePresenterMin);
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
            private readonly double _baseHostMin;
            private readonly double _basePresenterMin;
            private readonly double _baseBorderMin;
            private readonly double _baseOwnerMin;
            private bool _expanding;

            public ExpandState(
                ActivityDesigner owner,
                FrameworkElement host,
                WorkflowItemPresenter presenter,
                double basePresenterMin)
            {
                _owner = owner;
                _host = host;
                _presenter = presenter;
                _basePresenterMin = basePresenterMin;
                _baseHostMin = Math.Max(host.MinWidth, basePresenterMin);

                var border = owner.Content as Border;
                _baseBorderMin = border != null && border.MinWidth > 0 ? border.MinWidth : 320;
                _baseOwnerMin = owner.MinWidth > 0 ? owner.MinWidth : _baseBorderMin;
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
                    double contentWidth = 0;

                    if (nestedDesigner != null)
                    {
                        nestedDesigner.HorizontalAlignment = HorizontalAlignment.Center;
                        nestedDesigner.ClearValue(FrameworkElement.WidthProperty);
                        nestedDesigner.ClearValue(FrameworkElement.MaxWidthProperty);

                        // Keep nested item presenters from Stretch-inflating DesiredSize against an
                        // already-grown parent (feedback loop / huge side padding).
                        foreach (WorkflowItemsPresenter itemsPresenter in FindDescendantsOfType<WorkflowItemsPresenter>(nestedDesigner))
                        {
                            itemsPresenter.HorizontalAlignment = HorizontalAlignment.Center;
                            itemsPresenter.ClearValue(FrameworkElement.WidthProperty);
                            itemsPresenter.ClearValue(FrameworkElement.MaxWidthProperty);
                        }

                        nestedDesigner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        contentWidth = Math.Max(contentWidth, nestedDesigner.DesiredSize.Width);
                    }

                    _presenter.HorizontalAlignment = HorizontalAlignment.Center;
                    _presenter.HorizontalContentAlignment = HorizontalAlignment.Center;
                    _presenter.ClearValue(FrameworkElement.WidthProperty);
                    _presenter.ClearValue(FrameworkElement.MaxWidthProperty);
                    _presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    contentWidth = Math.Max(contentWidth, _presenter.DesiredSize.Width);

                    if (contentWidth <= 1 || double.IsNaN(contentWidth) || double.IsInfinity(contentWidth))
                    {
                        contentWidth = _basePresenterMin;
                    }

                    double needed = Math.Max(_basePresenterMin, contentWidth);
                    _host.MinWidth = Math.Max(_baseHostMin, needed);
                    _presenter.MinWidth = Math.Max(_basePresenterMin, Math.Min(needed, contentWidth > 1 ? contentWidth : _basePresenterMin));

                    var border = _owner.Content as Border;
                    if (border != null)
                    {
                        double borderNeeded = Math.Max(
                            _baseBorderMin,
                            needed + border.Padding.Left + border.Padding.Right);
                        border.MinWidth = borderNeeded;
                    }

                    _owner.ClearValue(FrameworkElement.MaxWidthProperty);
                    _owner.MinWidth = Math.Max(_baseOwnerMin, needed + 24);
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
