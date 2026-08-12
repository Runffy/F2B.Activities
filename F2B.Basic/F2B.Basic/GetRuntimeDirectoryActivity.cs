using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(GetRuntimeDirectoryDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Runtime Directory")]
    [Description("Returns the per-run runtime directory under OpenRPA ProjectsDirectory\\Runtime\\{sourceProject}\\{timestamp}. Always uses the outermost source workflow's project (follows Invoke OpenRPA caller chain). Mode controls timestamp precision. Expression: F2B.Basic.RuntimeDirectory.Path (Second).")]
    public sealed class GetRuntimeDirectoryActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetRuntimeDirectoryActivity()
        {
            DisplayName = "Get Runtime Directory";
            Mode = RuntimeDirectoryMode.Second;
        }

        [DisplayName("Mode")]
        [Description("Timestamp precision for the runtime folder name: Year, Month, Day, Hour, Minute, or Second.")]
        [Category("Input")]
        [DefaultValue(RuntimeDirectoryMode.Second)]
        public RuntimeDirectoryMode Mode { get; set; }

        [DisplayName("Runtime Directory")]
        [Description("Absolute path of the runtime directory for this workflow run.")]
        [Category("Output")]
        public OutArgument<string> RuntimeDirectory { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetRuntimeDirectoryActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            RuntimeDirectory.Set(
                context,
                global::F2B.Basic.RuntimeDirectory.GetOrCreate(context, Mode));
        }
    }
}
