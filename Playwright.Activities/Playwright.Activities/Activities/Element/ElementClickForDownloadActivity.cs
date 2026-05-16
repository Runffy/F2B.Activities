using System.Activities;
using System.ComponentModel;
using Microsoft.Playwright;

namespace Playwright.Activities
{
    [DisplayName("Element.ClickForDownload")]
    [TypeDescriptionProvider(typeof(ClickValidationTypeDescriptionProvider))]
    public sealed class ElementClickForDownloadActivity : ElementTargetActivityBase, IClickValidationConfig
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> SaveAsPath { get; set; }

        [Category("Input")]
        [DefaultValue(MouseButton.Left)]
        [TypeConverter("Playwright.Activities.MouseButtonTypeConverter, Playwright.Activities")]
        public MouseButton Button { get; set; } = MouseButton.Left;

        [Category("Input")]
        [DefaultValue(1)]
        public InArgument<int> Count { get; set; } = 1;

        [Category("Input")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [Category("Input")]
        public InArgument<string> ValidationSelector { get; set; }

        [Category("Input")]
        public InArgument<string[]> Modifiers { get; set; }

        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool Force { get; set; } = false;

        [Category("Input")]
        [DefaultValue(ClickValidateMode.None)]
        [TypeConverter("Playwright.Activities.ClickValidateModeTypeConverter, Playwright.Activities")]
        public ClickValidateMode Validate
        {
            get => _validate;
            set
            {
                _validate = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [Category("Output")]
        public OutArgument<DownloadInfo> Download { get; set; }

        private ClickValidateMode _validate = ClickValidateMode.None;

        protected override void Execute(CodeActivityContext context)
        {
            var download = ResolveTargetElement(context).ClickForDownload(
                saveAsPath: SaveAsPath == null ? null : SaveAsPath.Get(context),
                button: Button,
                count: ActivityArgumentHelper.GetOrDefault(Count, context, 1),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500),
                modifiers: Modifiers == null ? null : Modifiers.Get(context),
                force: Force,
                validate: Validate,
                validationSelector: ValidationSelector == null ? null : ValidationSelector.Get(context),
                timeout: ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            Download?.Set(context, download);
        }
    }
}
