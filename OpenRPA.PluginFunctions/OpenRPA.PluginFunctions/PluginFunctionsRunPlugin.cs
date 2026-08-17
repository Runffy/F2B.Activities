using System.Windows.Controls;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// OpenRPA runner plugin entry. Loaded from Documents\OpenRPA\extensions.
    /// </summary>
    public sealed class PluginFunctionsRunPlugin : ObservableObject, IRunPlugin
    {
        public static string PluginName
        {
            get { return "PluginFunctions"; }
        }

        public string Name
        {
            get { return PluginName; }
        }

        public UserControl editor
        {
            get { return null; }
        }

        public void Initialize(IOpenRPAClient client)
        {
            PluginContext.SetClient(client);
            ToolboxDoubleClickHook.Start();
            DesignerHotkeys.Start();
        }

        public bool onWorkflowStarting(ref IWorkflowInstance e, bool resumed)
        {
            return true;
        }

        public bool onWorkflowResumeBookmark(ref IWorkflowInstance e, string bookmarkName, object value)
        {
            return true;
        }

        public void onWorkflowCompleted(ref IWorkflowInstance e)
        {
        }

        public void onWorkflowAborted(ref IWorkflowInstance e)
        {
        }

        public void onWorkflowIdle(ref IWorkflowInstance e)
        {
        }
    }
}
