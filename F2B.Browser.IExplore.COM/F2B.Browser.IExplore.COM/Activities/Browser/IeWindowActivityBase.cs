using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("IE Window Activity Base")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public abstract class IeWindowActivityBase : CodeActivity
    {
        [Category("Input")]
        [DisplayName("Input Window")]
        [RequiredArgument]
        public InArgument<IEWindowController> InputWindow { get; set; }

        protected IEWindowController ResolveWindow(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new ArgumentException("InputWindow is required.");

            return window;
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (InputWindow == null || InputWindow.Expression == null)
            {
                metadata.AddValidationError("Input Window is required.");
            }
        }
    }
}
