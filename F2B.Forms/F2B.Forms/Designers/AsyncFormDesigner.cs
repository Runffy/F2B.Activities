using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Activities.Statements;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using F2B.Forms.Activities;

namespace F2B.Forms.Designers
{
    public sealed class AsyncFormDesigner : ActivityDesigner
    {
        public AsyncFormDesigner()
        {
            var root = new StackPanel();

            root.Children.Add(CreateSection(
                "Init Scope",
                "ModelItem.Init",
                "Drop init activities here (runs once after open)",
                expandedByDefault: false));

            root.Children.Add(CreateBindEventsSection());

            root.Children.Add(CreateSection(
                "Close Scope",
                "ModelItem.Close",
                "Drop close activities here (runs on user X / Closing). Call Close Form to dismiss.",
                expandedByDefault: false));

            root.Children.Add(new TextBlock
            {
                Text = "Form Path / Timeout → Property Grid. Defaults → Init Scope. User close → Close Scope.",
                FontSize = 10,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            Content = new Border
            {
                BorderBrush = Brushes.DarkSlateGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                MinWidth = 360,
                Child = root
            };
        }

        private FrameworkElement CreateBindEventsSection()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            panel.Children.Add(new TextBlock
            {
                Text = "Bind Events",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var itemsPresenter = new WorkflowItemsPresenter
            {
                HintText = "Bind Event handlers",
                MinWidth = 320,
                Margin = new Thickness(4, 0, 0, 0),
                SpacerTemplate = CreateSealedSpacerTemplate(),
                ItemsPanel = CreateSealedItemsPanel()
            };
            // BindEvents is get-only Collection<> — must bind the ModelItemCollection, OneWay.
            BindingOperations.SetBinding(
                itemsPresenter,
                WorkflowItemsPresenter.ItemsProperty,
                new Binding("ModelItem.Properties[BindEvents].Collection")
                {
                    Mode = BindingMode.OneWay
                });

            panel.Children.Add(itemsPresenter);

            var addButton = new Button
            {
                Content = "Add Bind Event",
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            addButton.Click += (s, e) => AddBindEvent();
            panel.Children.Add(addButton);

            return panel;
        }

        private void AddBindEvent()
        {
            ModelItemCollection collection = ModelItem.Properties["BindEvents"].Collection;
            var bind = new BindEventActivity
            {
                DisplayName = "Bind Event",
                UiBehavior = new InArgument<string>("NoLock"),
                Handler = new Sequence { DisplayName = "Handler" }
            };
            collection.Add(bind);
        }

        private static DataTemplate CreateSealedSpacerTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.HeightProperty, 4.0);
            var template = new DataTemplate { VisualTree = factory };
            template.Seal();
            return template;
        }

        private static ItemsPanelTemplate CreateSealedItemsPanel()
        {
            var template = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
            template.Seal();
            return template;
        }

        private static FrameworkElement CreateSection(
            string title,
            string bindingPath,
            string hint,
            bool expandedByDefault)
        {
            var presenter = new WorkflowItemPresenter
            {
                HintText = hint,
                MinWidth = 320,
                MinHeight = 40,
                Margin = new Thickness(4, 2, 0, 0)
            };
            BindingOperations.SetBinding(presenter, WorkflowItemPresenter.ItemProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay
            });

            var expander = new Expander
            {
                Header = title,
                IsExpanded = expandedByDefault,
                Margin = new Thickness(0, 0, 0, 4),
                Content = presenter
            };
            return expander;
        }
    }
}
