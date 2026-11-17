using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("JSON")]
    [DisplayName("Dump JSON")]
    [Description("Write Dictionary<string, object> to a JSON file. Mode=Overwrite replaces the file; Mode=Update merges top-level keys (create file when missing).")]
    public sealed class DumpJsonActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public DumpJsonActivity()
        {
            DisplayName = "Dump JSON";
            Mode = JsonDumpMode.Update;
        }

        [DisplayName("File Path")]
        [Description("Absolute or relative path of the JSON file to write.")]
        [Category("Input")]
        [RequiredArgument]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("JSON")]
        [Description("Dictionary to write or merge into the JSON file.")]
        [Category("Input")]
        [RequiredArgument]
        public InArgument<Dictionary<string, object>> Json { get; set; }

        [DisplayName("Mode")]
        [Description("Update: merge top-level keys into existing file (or create if missing). Overwrite: replace the entire file.")]
        [Category("Input")]
        [DefaultValue(JsonDumpMode.Update)]
        public JsonDumpMode Mode { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new DumpJsonActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string filePath = FilePath.Get(context);
            Dictionary<string, object> data = Json.Get(context);
            JsonFileHelper.Dump(filePath, data, Mode);
        }
    }
}
