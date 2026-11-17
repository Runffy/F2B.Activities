using System.Activities;
using System.ComponentModel;

namespace F2B.Basic
{
    [Designer(typeof(BasicGenericActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("JSON")]
    [DisplayName("Load JSON Key")]
    [Description("Load one top-level key from a JSON object file. Change ArgumentType to set Result type (same pattern as CXML Worksheet-ReadCell).")]
    [System.Activities.Presentation.DefaultTypeArgument(typeof(string))]
    public sealed class LoadJsonKeyActivity<TResult> : CodeActivity<TResult>
    {
        public LoadJsonKeyActivity()
        {
            DisplayName = "Load JSON Key";
        }

        [DisplayName("File Path")]
        [Description("Absolute or relative path of the JSON file.")]
        [Category("Input")]
        [RequiredArgument]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Key")]
        [Description("Top-level JSON object key to read.")]
        [Category("Input")]
        [RequiredArgument]
        public InArgument<string> Key { get; set; }

        protected override TResult Execute(CodeActivityContext context)
        {
            string filePath = FilePath.Get(context);
            string key = Key.Get(context);
            object raw = JsonFileHelper.GetKeyValue(filePath, key);
            return JsonFileHelper.ConvertJsonValueTo<TResult>(raw);
        }
    }
}
