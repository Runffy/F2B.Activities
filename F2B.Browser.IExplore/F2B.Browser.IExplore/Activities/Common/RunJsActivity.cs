using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Run JavaScript")]
    [Description("Execute JavaScript on the window document or on an element (handle or locator).")]
    [Designer(typeof(IeRunJsActivityDesigner))]
    public sealed class RunJsActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Base On")]
        [Description("Run script in the window/frame document, or on an element.")]
        [Category("Input")]
        [DefaultValue(IeBaseOn.Window)]
        public IeBaseOn BaseOn { get; set; } = IeBaseOn.Window;

        [DisplayName("Script")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Script { get; set; }

        [DisplayName("Element")]
        [Description("Existing element handle (Base On = Element). Omit to locate via Element (Json String).")]
        [Category("Input")]
        public InArgument<IEHtmlElement> Element { get; set; }

        [DisplayName("Element (Json String)")]
        [Description("Locator used to find the script target when Element is not set.")]
        [Category("Input")]
        public InArgument<string> ElementJson { get; set; }

        [DisplayName("Frame (Json String)")]
        [Description("Frame path for document context (Window) or element frame (Element).")]
        [Category("Input")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Args (Json String)")]
        [Description("JSON array passed as args, e.g. ['a','b']")]
        [Category("Input")]
        public InArgument<string> ArgsJson { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout when locating element by JSON (Base On = Element, no Element handle).")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Result")]
        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            var script = Script.Get(context);
            var argsJson = ArgsJson == null ? null : ArgsJson.Get(context);
            var framePath = IeLocatorHelper.GetFramePath(context, FramePath);
            IeScriptResult scriptResult;

            if (BaseOn == IeBaseOn.Window)
            {
                if (string.IsNullOrWhiteSpace(framePath))
                    scriptResult = window.ExecuteScript(script, null, null, argsJson);
                else
                {
                    var frameLocator = IeLocatorHelper.BuildLocator("{'tag':'html'}", framePath);
                    scriptResult = window.ExecuteScript(script, frameLocator, null, argsJson);
                }
            }
            else
            {
                var element = Element == null ? null : Element.Get(context);
                if (element != null)
                {
                    IELocator frameLocator = string.IsNullOrWhiteSpace(framePath)
                        ? null
                        : IeLocatorHelper.BuildLocator("{'tag':'html'}", framePath);
                    scriptResult = window.ExecuteScript(script, frameLocator, element, argsJson);
                }
                else
                {
                    var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
                    var locator = IeLocatorHelper.BuildLocator(
                        IeLocatorHelper.GetElementJson(context, ElementJson),
                        framePath);
                    scriptResult = window.ExecuteScript(script, locator, argsJson, timeout);
                }
            }

            Result?.Set(context, scriptResult == null ? null : scriptResult.Raw);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (InputWindow == null || InputWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");
            if (Script == null || Script.Expression == null)
                metadata.AddValidationError("Script is required.");

            if (BaseOn == IeBaseOn.Element)
            {
                var hasElement = Element != null && Element.Expression != null;
                var hasJson = ElementJson != null && ElementJson.Expression != null;
                if (!hasElement && !hasJson)
                    metadata.AddValidationError("Element or Element (Json String) is required when Base On = Element.");
            }
        }
    }
}
