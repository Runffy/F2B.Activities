using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Resource Path")]
    [Description("Returns {ProjectsDirectory}/Projects/{sourceProject}. Always uses the outermost source workflow's project (Invoke OpenRPA caller chain). Expression: F2B.Basic.ResourceDirectory.Path")]
    public sealed class GetResourcePathActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetResourcePathActivity()
        {
            DisplayName = "Get Resource Path";
        }

        [DisplayName("Resource Path")]
        [Description("Absolute path: ProjectsDirectory\\Projects\\{sourceProjectName}.")]
        [Category("Output")]
        public OutArgument<string> ResourcePath { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetResourcePathActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string path = ResourceDirectory.GetOrCreate(context);
            ResourcePath?.Set(context, path);
        }
    }
}
