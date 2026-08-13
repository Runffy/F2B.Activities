using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Model;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Child Controls")]
    [Description("List child controls under a container, grouped by type as Dictionary<string, string[]>. Use Deep Dive for nested containers. Type Filter (Flags) is a multi-select in the property pane; leave None/All for every type.")]
    public sealed class GetChildControlsActivity : CodeActivity<Dictionary<string, string[]>>
    {
        public GetChildControlsActivity()
        {
            DisplayName = "Get Child Controls";
            DeepDive = true;
            TypeFilter = FormControlTypeFilter.None;
        }

        [RequiredArgument]
        [DisplayName("Container Id")]
        [Description("Parent control Id. Use \"form\" (or the form's Id) for the root form. Typical containers: Panel, GroupBox, TabPage, ScrollContainer, TableLayout, TabControl.")]
        [Category("Input.A")]
        public InArgument<string> ContainerId { get; set; }

        [DisplayName("Deep Dive")]
        [Description("True = recurse into nested containers; False = direct children only.")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public bool DeepDive { get; set; }

        [DisplayName("Type Filter")]
        [Description("Multi-select control types in the property pane. Leave (All / None) to include every type.")]
        [Category("Input.C")]
        [DefaultValue(FormControlTypeFilter.None)]
        [Editor(typeof(FormControlTypeFilterEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public FormControlTypeFilter TypeFilter { get; set; }

        [DisplayName("Type Filter (Runtime)")]
        [Description("Optional expression string[] of type names (e.g. {\"Button\",\"TextBox\"}). Empty/null = no extra filter. Combined with Type Filter flags.")]
        [Category("Input.C")]
        public InArgument<string[]> TypeFilterRuntime { get; set; }

        [DisplayName("Controls By Type")]
        [Description("Dictionary: key = control type name, value = Id array of that type.")]
        [Category("Output")]
        public OutArgument<Dictionary<string, string[]>> ControlsByType { get; set; }

        [DisplayName("Type Count")]
        [Description("Number of distinct types in the result (dictionary key count).")]
        [Category("Output")]
        public OutArgument<int> TypeCount { get; set; }

        protected override Dictionary<string, string[]> Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string containerId = ContainerId.Get(context);
            string[] runtimeFilter = TypeFilterRuntime == null ? null : TypeFilterRuntime.Get(context);

            Dictionary<string, string[]> grouped = session.GetChildControlsByType(
                containerId,
                DeepDive,
                TypeFilter,
                runtimeFilter);

            ControlsByType?.Set(context, grouped);
            TypeCount?.Set(context, grouped == null ? 0 : grouped.Count);
            return grouped;
        }
    }
}
