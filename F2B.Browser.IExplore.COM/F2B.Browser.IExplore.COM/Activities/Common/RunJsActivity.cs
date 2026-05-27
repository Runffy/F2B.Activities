using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Run JavaScript")]
    [Description("Execute JavaScript in current document/frame.")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public sealed class RunJsActivity : CodeActivity
    {
        [Category("Input")]
        [DisplayName("Input Window")]
        [RequiredArgument]
        public InArgument<IEWindowController> InputWindow { get; set; }

        [Category("Input")]
        [DisplayName("Script")]
        [RequiredArgument]
        public InArgument<string> Script { get; set; }

        [Category("Input")]
        [DisplayName("Frame Path (Json String)")]
        public InArgument<string> FramePath { get; set; }

        [Category("Input")]
        [DisplayName("Args (Json String)")]
        public InArgument<string> Args { get; set; }

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Result")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new ArgumentException("InputWindow is required.");

            var script = Script == null ? null : Script.Get(context);
            var framePathJson = FramePath == null ? null : FramePath.Get(context);
            var argsJson = Args == null ? null : Args.Get(context);
            var framePath = ActivityArgumentHelper.ParseJsonArray(framePathJson, "Frame Path", required: false);
            var args = ActivityArgumentHelper.ParseJsonArray(argsJson, "Args", required: false);
            var result = window.run_js(script, framePath, args);
            Result.Set(context, result);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (InputWindow == null || InputWindow.Expression == null)
            {
                metadata.AddValidationError("Input Window is required.");
            }

            if (Script == null || Script.Expression == null)
            {
                metadata.AddValidationError("Script is required.");
            }
        }
    }
}
