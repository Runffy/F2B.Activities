using System;
using System.Activities;
using System.Activities.Presentation.PropertyEditing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    /// <summary>
    /// Compiles a standalone C# Program.cs (usings, helpers, Main) and invokes Main.
    /// Workflow Imports are ignored. In/Out/InOut values are passed through a generated Args type.
    /// </summary>
    [Designer(typeof(InvokeCSharpCodeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Invoke C# Code")]
    [Description("Run a standalone C# Program.cs. Write usings and functions in Code. Workflow calls Main(Args) or Main(). Map In/Out/InOut on Arguments.")]
    public sealed class InvokeCSharpCodeActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public const string DefaultCode =
@"// Import your namespace here.
// using ...;

public static class Program
{
    public static void Main(Args args)
    {
        // Write your logic here.
    }
}
";

        public InvokeCSharpCodeActivity()
        {
            DisplayName = "Invoke C# Code";
            Code = new InArgument<string>(DefaultCode);
            Arguments = new Dictionary<string, Argument>();

            var builder = new System.Activities.Presentation.Metadata.AttributeTableBuilder();
            builder.AddCustomAttributes(
                typeof(InvokeCSharpCodeActivity),
                "Arguments",
                new EditorAttribute(
                    typeof(OpenRPA.Interfaces.Activities.ArgumentCollectionEditor),
                    typeof(PropertyValueEditor)));
            builder.AddCustomAttributes(
                typeof(InvokeCSharpCodeActivity),
                "Code",
                new EditorAttribute(
                    typeof(InvokeCSharpCodePropertyEditor),
                    typeof(PropertyValueEditor)));
            System.Activities.Presentation.Metadata.MetadataStore.AddAttributeTable(builder.CreateTable());
        }

        [RequiredArgument]
        [DisplayName("Code")]
        [Description("Full C# source (like Program.cs). Use the property-grid ... button or Edit Code on the canvas.")]
        [Category("Input.A")]
        [Editor(typeof(InvokeCSharpCodePropertyEditor), typeof(PropertyValueEditor))]
        public InArgument<string> Code { get; set; }

        [DisplayName("Arguments")]
        [Description("In / Out / InOut arguments exposed as properties on the generated Args class.")]
        [Category("Input.B")]
        [Browsable(true)]
        public Dictionary<string, Argument> Arguments { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new InvokeCSharpCodeActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string code = Code != null ? Code.Get(context) : null;
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Invoke C# Code: Code is empty.");
            }

            InvokeCSharpCodeHost.Run(code, Arguments, context);
        }
    }
}
