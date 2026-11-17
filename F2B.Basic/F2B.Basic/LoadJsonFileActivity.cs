using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("JSON")]
    [DisplayName("Load JSON File")]
    [Description("Load a JSON object file into Dictionary<string, object>.")]
    public sealed class LoadJsonFileActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public LoadJsonFileActivity()
        {
            DisplayName = "Load JSON File";
        }

        [DisplayName("File Path")]
        [Description("Absolute or relative path of the JSON file.")]
        [Category("Input")]
        [RequiredArgument]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("JSON")]
        [Description("Loaded JSON object as Dictionary<string, object>. Nested objects become nested dictionaries; arrays become List<object>.")]
        [Category("Output")]
        public OutArgument<Dictionary<string, object>> Json { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new LoadJsonFileActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string filePath = FilePath.Get(context);
            Dictionary<string, object> data = JsonFileHelper.LoadDictionary(filePath);
            Json?.Set(context, data);
        }
    }
}
