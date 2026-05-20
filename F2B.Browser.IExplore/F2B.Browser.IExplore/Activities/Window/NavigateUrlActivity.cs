using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Navigate Url")]
    [Description("Navigate the IE window to a URL.")]
    public sealed class NavigateUrlActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Url")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            window.Navigate(Url.Get(context));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (InputWindow == null || InputWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");
            if (Url == null || Url.Expression == null)
                metadata.AddValidationError("Url is required.");
        }
    }
}
