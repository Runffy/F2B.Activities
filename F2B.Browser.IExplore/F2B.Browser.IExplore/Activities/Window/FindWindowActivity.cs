using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    [DisplayName("Find Window")]
    [Description("Connect to an existing IE window by title/url/class filters.")]
    public sealed class FindWindowActivity : CodeActivity
    {
        [DisplayName("Title Contains")]
        [Category("Input")]
        public InArgument<string> TitleContains { get; set; }

        [DisplayName("Url Contains")]
        [Category("Input")]
        public InArgument<string> UrlContains { get; set; }

        [DisplayName("Class Name")]
        [Category("Input")]
        public InArgument<string> ClassName { get; set; }

        [DisplayName("Match Index")]
        [Category("Input")]
        [DefaultValue(0)]
        public InArgument<int> MatchIndex { get; set; } = 0;

        [DisplayName("Timeout (ms)")]
        [Category("Input")]
        [DefaultValue(45000)]
        public InArgument<int> Timeout { get; set; } = 45000;

        [DisplayName("Apply IE Policy")]
        [Description("Apply localhost IE automation policy before connect.")]
        [Category("Input")]
        [DefaultValue(true)]
        public bool ApplyIePolicy { get; set; } = true;

        [DisplayName("IE Window")]
        [Category("Output")]
        public OutArgument<EmbeddedIEWindow> OutputWindow { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            if (ApplyIePolicy)
                IeSecurityConfigurator.ApplyAutomationPolicy();

            var criteria = BuildCriteria(context);
            if (criteria.Count == 0)
                throw new InvalidOperationException("At least one of Title Contains, Url Contains, or Class Name must be provided.");

            var index = ActivityArgumentHelper.GetOrDefault(MatchIndex, context, 0);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, 45000);
            var window = EmbeddedIExplore.Connect(criteria, index, timeout);
            OutputWindow?.Set(context, window);
        }

        private Dictionary<string, string> BuildCriteria(CodeActivityContext context)
        {
            var criteria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var title = TitleContains == null ? null : TitleContains.Get(context);
            var url = UrlContains == null ? null : UrlContains.Get(context);
            var cls = ClassName == null ? null : ClassName.Get(context);

            if (!string.IsNullOrWhiteSpace(title))
                criteria[IEConnectCriteria.Title] = title.Trim();
            if (!string.IsNullOrWhiteSpace(url))
                criteria[IEConnectCriteria.Url] = url.Trim();
            if (!string.IsNullOrWhiteSpace(cls))
                criteria[IEConnectCriteria.ClassName] = cls.Trim();

            return criteria;
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (OutputWindow == null)
                metadata.AddValidationError("IE Window output is required.");
        }
    }
}
