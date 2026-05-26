using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Open WS")]
    [Description("Launch a PCOMM workstation profile (.ws) using PCSWS.")]
    public sealed class OpenWsActivity : CodeActivity
    {
        public OpenWsActivity()
        {
            DisplayName = "Open WS";
        }

        [DisplayName("WS File Path")]
        [Description("Absolute or relative path to the .ws file.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> WsFilePath { get; set; }

        [DisplayName("IBM Path")]
        [Description("IBM Personal Communications installation directory, or the full path to pcsws.exe.")]
        [Category("Input")]
        [DefaultValue(PcommWsLauncher.DefaultIbmPath)]
        public InArgument<string> IbmPath { get; set; } = PcommWsLauncher.DefaultIbmPath;

        protected override void Execute(CodeActivityContext context)
        {
            var wsFilePath = WsFilePath.Get(context);
            var ibmPath = IbmPath == null || IbmPath.Expression == null
                ? PcommWsLauncher.DefaultIbmPath
                : IbmPath.Get(context);

            PcommWsLauncher.OpenWs(wsFilePath, ibmPath);
        }
    }
}
