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
    /// to a fixed presenter width. Width follows content and can shrink again after paste/replace.
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

            double baselinePresenterMinWidth = presenter.MinWidth > 0 ? presenter.MinWidth : 280;

            var host = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = baselinePresenterMinWidth,
                MinHeight = Math.Max(40, presenter.MinHeight)
            };

            presenter.HorizontalAlignment = HorizontalAlignment.Center;
            presenter.HorizontalContentAlignment = HorizontalAlignment.Center;

            var state = new ExpandState(owner, host, presenter, baselinePresenterMinWidth);
            Action apply = state.Apply;

            // Do NOT hook host.SizeChanged: Stretch-inside-wide-parent would ratchet MinWidth
            // up to the parent width and never recover (Only If / Only Try paste bug).

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
                    // Item replaced (e.g. Ctrl+V Sequence): drop inflated MinWidth first, then measure.
                    state.ResetToBaseline();
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
            private readonly double _baselinePresenterMinWidth;
            private readonly double _baselineOwnerMinWidth;
            private ActivityDesigner _nestedSizeSource;
            private bool _expanding;

            public ExpandState(
                ActivityDesigner owner,
                FrameworkElement host,
                WorkflowItemPresenter presenter,
                double baselinePresenterMinWidth)
            {
                _owner = owner;
                _host = host;
                _presenter = presenter;
                _baselinePresenterMinWidth = baselinePresenterMinWidth;
                _baselineOwnerMinWidth = Math.Max(
                    owner.MinWidth > 0 ? owner.MinWidth : 0,
                    baselinePresenterMinWidth + 24);
            }

            public void ResetToBaseline()
            {
                _host.MinWidth = _baselinePresenterMinWidth;

                Border border = FindContentBorder(_owner);
                if (border != null)
                {
                    border.MinWidth = _baselinePresenterMinWidth + border.Padding.Left + border.Padding.Right;
                }

                _owner.MinWidth = _baselineOwnerMinWidth;
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
                    AttachNestedSizeListener(nestedDesigner);

                    double needed = _baselinePresenterMinWidth;

                    if (nestedDesigner != null)
                    {
                        nestedDesigner.HorizontalAlignment = HorizontalAlignment.Center;
                        nestedDesigner.ClearValue(FrameworkElement.WidthProperty);
                        nestedDesigner.ClearValue(FrameworkElement.MaxWidthProperty);
                        nestedDesigner.ClearValue(FrameworkElement.MinWidthProperty);

                        foreach (WorkflowItemsPresenter itemsPresenter in FindDescendantsOfType<WorkflowItemsPresenter>(nestedDesigner))
                        {
                            itemsPresenter.HorizontalAlignment = HorizontalAlignment.Center;
                            itemsPresenter.ClearValue(FrameworkElement.WidthProperty);
                            itemsPresenter.ClearValue(FrameworkElement.MaxWidthProperty);
                            itemsPresenter.ClearValue(FrameworkElement.MinWidthProperty);
                        }

                        nestedDesigner.InvalidateMeasure();
                        nestedDesigner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        needed = Math.Max(needed, nestedDesigner.DesiredSize.Width);
                    }

                    _presenter.ClearValue(FrameworkElement.WidthProperty);
                    _presenter.ClearValue(FrameworkElement.MaxWidthProperty);
                    _presenter.InvalidateMeasure();
                    _presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    needed = Math.Max(needed, _presenter.DesiredSize.Width);

                    // Reject stretch-to-parent artifacts: if measure ≈ current stretched host width and
                    // that is far above baseline, prefer nested content width only.
                    double hostActual = _host.ActualWidth;
                    if (hostActual > _baselinePresenterMinWidth + 32
                        && needed + 1 >= hostActual
                        && nestedDesigner != null)
                    {
                        needed = Math.Max(_baselinePresenterMinWidth, nestedDesigner.DesiredSize.Width);
                    }

                    if (needed <= 1)
                    {
                        needed = _baselinePresenterMinWidth;
                    }

                    // Always assign (allow shrink after inflated paste / item replace).
                    _host.MinWidth = Math.Max(_baselinePresenterMinWidth, needed);

                    Border border = FindContentBorder(_owner);
                    if (border != null)
                    {
                        double borderNeeded = needed + border.Padding.Left + border.Padding.Right;
                        border.MinWidth = borderNeeded;
                    }

                    _owner.ClearValue(FrameworkElement.MaxWidthProperty);
                    double designerNeeded = Math.Max(_baselineOwnerMinWidth, needed + 24);
                    _owner.MinWidth = designerNeeded;
                }
                finally
                {
                    _expanding = false;
                }
            }

            private void AttachNestedSizeListener(ActivityDesigner nestedDesigner)
            {
                if (ReferenceEquals(_nestedSizeSource, nestedDesigner))
                {
                    return;
                }

                if (_nestedSizeSource != null)
                {
                    _nestedSizeSource.SizeChanged -= OnNestedSizeChanged;
                }

                _nestedSizeSource = nestedDesigner;
                if (_nestedSizeSource != null)
                {
                    _nestedSizeSource.SizeChanged += OnNestedSizeChanged;
                }
            }

            private void OnNestedSizeChanged(object sender, SizeChangedEventArgs e)
            {
                if (!e.WidthChanged || Math.Abs(e.PreviousSize.Width - e.NewSize.Width) < 0.5)
                {
                    return;
                }

                // Content-driven growth/shrink (adding wide children). Not host stretch.
                _presenter.Dispatcher.BeginInvoke((Action)Apply, DispatcherPriority.ContextIdle);
            }
        }

        private static Border FindContentBorder(ActivityDesigner owner)
        {
            if (owner == null)
            {
                return null;
            }

            if (owner.Content is Border border)
            {
                return border;
            }

            if (owner.Content is ContentControl contentControl && contentControl.Content is Border nested)
            {
                return nested;
            }

            return null;
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
