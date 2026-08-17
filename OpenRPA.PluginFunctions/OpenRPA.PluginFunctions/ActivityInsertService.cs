using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.Services;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Creates activities and inserts them via IDesigner.AddActivity (same as Toolbox double-click).
    /// </summary>
    internal static class ActivityInsertService
    {
        public static Activity CreateActivity(Type type)
        {
            if (type == null || type.IsAbstract)
            {
                return null;
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }

            if (instance is IActivityTemplateFactory factory)
            {
                return factory.Create(null);
            }

            return instance as Activity;
        }

        public static bool TryAddActivity(Type type)
        {
            Activity activity = CreateActivity(type);
            if (activity == null)
            {
                return false;
            }

            IDesigner designer = PluginContext.ResolveDesigner();
            if (designer == null)
            {
                return false;
            }

            Exception error = null;
            GenericTools.RunUI(() =>
            {
                try
                {
                    designer.AddActivity(activity);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }, 10000);

            return error == null;
        }

        public static bool TryFocusModelItem(IDesigner designer, ModelItem item)
        {
            if (designer == null || item == null)
            {
                return false;
            }

            try
            {
                Selection.SelectOnly(designer.WorkflowDesigner.Context, item);
                item.Focus(20);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<ModelItem> GetAllActivities(IDesigner designer)
        {
            var list = new List<ModelItem>();
            if (designer == null || designer.WorkflowDesigner == null)
            {
                return list;
            }

            try
            {
                ModelService modelService = designer.WorkflowDesigner.Context.Services.GetService<ModelService>();
                if (modelService == null)
                {
                    return list;
                }

                list.AddRange(modelService.Find(modelService.Root, typeof(Activity)));
            }
            catch
            {
            }

            return list;
        }

        public static string GetDisplayName(ModelItem item)
        {
            if (item == null)
            {
                return null;
            }

            try
            {
                ModelProperty prop = item.Properties["DisplayName"];
                if (prop != null && prop.ComputedValue != null)
                {
                    string text = Convert.ToString(prop.ComputedValue);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }

                object current = item.GetCurrentValue();
                Activity activity = current as Activity;
                if (activity != null && !string.IsNullOrWhiteSpace(activity.DisplayName))
                {
                    return activity.DisplayName.Trim();
                }

                return item.ItemType != null ? item.ItemType.Name : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
