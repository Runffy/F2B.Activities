using System;
using System.Activities;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using OpenRPA;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    /// <summary>
    /// Clears the OpenRPA dock Output panel (same effect as clicking the Output "X").
    /// Optionally also clears the Trace panel.
    /// </summary>
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Clear Output")]
    [Description("Clear the OpenRPA Output panel text. Optionally clear Trace as well. Place where you want a fresh Output view.")]
    public sealed class ClearOutputActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ClearOutputActivity()
        {
            DisplayName = "Clear Output";
            ClearOutputPanel = true;
            ClearTracePanel = false;
        }

        [DisplayName("Clear Output")]
        [Description("Clear the Output dock text (default true).")]
        [Category("Input")]
        [DefaultValue(true)]
        public bool ClearOutputPanel { get; set; }

        [DisplayName("Clear Trace")]
        [Description("Also clear the Trace dock text (default false).")]
        [Category("Input")]
        [DefaultValue(false)]
        public bool ClearTracePanel { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ClearOutputActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            if (!ClearOutputPanel && !ClearTracePanel)
            {
                return;
            }

            Exception error = null;
            GenericTools.RunUI(() =>
            {
                try
                {
                    ClearPanels(ClearOutputPanel, ClearTracePanel);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }, 15000);

            if (error != null)
            {
                throw new InvalidOperationException("Clear Output failed: " + error.Message, error);
            }
        }

        private static void ClearPanels(bool clearOutput, bool clearTrace)
        {
            object window = RobotInstance.instance != null ? RobotInstance.instance.Window : null;
            if (window == null)
            {
                throw new InvalidOperationException("OpenRPA main window is not available.");
            }

            PropertyInfo tracingProperty = window.GetType().GetProperty(
                "Tracing",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            object tracing = tracingProperty != null ? tracingProperty.GetValue(window) : null;
            if (tracing == null)
            {
                throw new InvalidOperationException("OpenRPA Tracing object was not found on the main window.");
            }

            Type tracingType = tracing.GetType();
            if (clearOutput)
            {
                SetStringProperty(tracingType, tracing, "OutputMessages", string.Empty);
            }

            if (clearTrace)
            {
                SetStringProperty(tracingType, tracing, "TraceMessages", string.Empty);
            }
        }

        private static void SetStringProperty(Type type, object target, string propertyName, string value)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null || !property.CanWrite)
            {
                throw new InvalidOperationException("Property '" + propertyName + "' is not writable on Tracing.");
            }

            property.SetValue(target, value);
        }
    }
}
