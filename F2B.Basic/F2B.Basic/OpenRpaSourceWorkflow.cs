using System;
using System.Collections.Generic;
using System.Reflection;

namespace F2B.Basic
{
    /// <summary>
    /// Shared OpenRPA "source run" resolution for Global, RuntimeDirectory, and ResourceDirectory.
    /// Walks WorkflowInstance.caller (and InvokeOpenRPA parent bookmarks) to the outermost run.
    /// </summary>
    public static class OpenRpaSourceWorkflow
    {
        private const int MaxCallerDepth = 32;

        public static bool TryResolve(
            System.Activities.CodeActivityContext context,
            out string sourceInstanceId,
            out string sourceProjectName)
        {
            if (context == null)
            {
                return TryResolveCurrent(out sourceInstanceId, out sourceProjectName);
            }

            return TryResolve(
                context.WorkflowInstanceId.ToString(),
                projectNameHint: null,
                out sourceInstanceId,
                out sourceProjectName);
        }

        public static bool TryResolveCurrent(out string sourceInstanceId, out string sourceProjectName)
        {
            sourceInstanceId = null;
            sourceProjectName = null;

            object instance = FindCurrentWorkflowInstance();
            if (instance == null)
            {
                return false;
            }

            string localId = GetStringPropertyValue(instance, "InstanceId");
            string localProject = GetStringPropertyValue(instance, "projectname", "ProjectName");
            return TryResolve(localId, localProject, out sourceInstanceId, out sourceProjectName);
        }

        public static bool TryResolve(
            string localInstanceId,
            string projectNameHint,
            out string sourceInstanceId,
            out string sourceProjectName)
        {
            ResolveRootWorkflow(localInstanceId, projectNameHint, out sourceInstanceId, out sourceProjectName);
            return !string.IsNullOrWhiteSpace(sourceInstanceId);
        }

        public static string ResolveSourceInstanceId(System.Activities.CodeActivityContext context)
        {
            string instanceId;
            string projectName;
            TryResolve(context, out instanceId, out projectName);
            return string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
        }

        public static string ResolveSourceInstanceId()
        {
            string instanceId;
            string projectName;
            TryResolveCurrent(out instanceId, out projectName);
            return string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
        }

        public static string ResolveSourceProjectName(System.Activities.CodeActivityContext context)
        {
            string instanceId;
            string projectName;
            TryResolve(context, out instanceId, out projectName);
            return string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        }

        public static string ResolveSourceProjectName()
        {
            string instanceId;
            string projectName;
            TryResolveCurrent(out instanceId, out projectName);
            return string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        }

        public static string ResolveSourceInstanceIdFromLocal(string localInstanceId)
        {
            string rootInstanceId;
            string unusedProject;
            ResolveRootWorkflow(localInstanceId, projectNameHint: null, out rootInstanceId, out unusedProject);
            return string.IsNullOrWhiteSpace(rootInstanceId) ? string.Empty : rootInstanceId.Trim();
        }

        public static bool IsSourceInstanceActive(string sourceInstanceId)
        {
            if (string.IsNullOrWhiteSpace(sourceInstanceId))
            {
                return false;
            }

            object instance = FindWorkflowInstanceById(sourceInstanceId);
            if (instance == null)
            {
                return false;
            }

            bool? completed = GetBoolPropertyValue(instance, "isCompleted");
            return completed != true;
        }

        public static HashSet<string> GetActiveSourceInstanceIds()
        {
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return active;
            }

            foreach (object item in instances)
            {
                if (item == null)
                {
                    continue;
                }

                bool? completed = GetBoolPropertyValue(item, "isCompleted");
                if (completed == true)
                {
                    continue;
                }

                string localId = GetStringPropertyValue(item, "InstanceId");
                string rootKey = ResolveSourceInstanceIdFromLocal(localId);
                if (!string.IsNullOrWhiteSpace(rootKey))
                {
                    active.Add(rootKey);
                }
            }

            return active;
        }

        private static void ResolveRootWorkflow(
            string workflowInstanceId,
            string projectNameHint,
            out string rootInstanceId,
            out string rootProjectName)
        {
            rootInstanceId = string.IsNullOrWhiteSpace(workflowInstanceId)
                ? null
                : workflowInstanceId.Trim();
            rootProjectName = projectNameHint;

            object instance = FindWorkflowInstanceById(rootInstanceId);
            if (instance == null)
            {
                instance = FindCurrentWorkflowInstance();
            }

            object root = FindRootCallerInstance(instance) ?? instance;

            string localId = instance == null ? rootInstanceId : GetStringPropertyValue(instance, "InstanceId");
            string climbedId = root == null ? null : GetStringPropertyValue(root, "InstanceId");
            if (string.IsNullOrWhiteSpace(climbedId)
                || string.Equals(climbedId, localId, StringComparison.OrdinalIgnoreCase))
            {
                string soleRoot = TryGetSoleActiveOutermostInstanceId();
                if (!string.IsNullOrWhiteSpace(soleRoot)
                    && !string.Equals(soleRoot, localId, StringComparison.OrdinalIgnoreCase))
                {
                    object soleInstance = FindWorkflowInstanceById(soleRoot);
                    if (soleInstance != null)
                    {
                        root = soleInstance;
                    }
                }
            }

            if (root == null)
            {
                return;
            }

            string id = GetStringPropertyValue(root, "InstanceId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                rootInstanceId = id.Trim();
            }

            string project = GetStringPropertyValue(root, "projectname", "ProjectName");
            if (!string.IsNullOrWhiteSpace(project))
            {
                rootProjectName = project;
            }
        }

        private static string TryGetSoleActiveOutermostInstanceId()
        {
            var outermost = new List<string>();
            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return null;
            }

            foreach (object item in instances)
            {
                if (item == null)
                {
                    continue;
                }

                bool? completed = GetBoolPropertyValue(item, "isCompleted");
                if (completed == true)
                {
                    continue;
                }

                string caller = GetStringPropertyValue(item, "caller", "Caller", "callerid", "CallerId");
                if (string.IsNullOrWhiteSpace(caller))
                {
                    caller = FindCallerInstanceIdByBookmark(item);
                }

                if (!string.IsNullOrWhiteSpace(caller))
                {
                    continue;
                }

                string id = GetStringPropertyValue(item, "InstanceId");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    outermost.Add(id.Trim());
                }
            }

            return outermost.Count == 1 ? outermost[0] : null;
        }

        private static object FindRootCallerInstance(object startInstance)
        {
            object current = startInstance;
            for (int i = 0; i < MaxCallerDepth && current != null; i++)
            {
                string callerId = GetStringPropertyValue(current, "caller", "Caller", "callerid", "CallerId");
                if (string.IsNullOrWhiteSpace(callerId))
                {
                    callerId = FindCallerInstanceIdByBookmark(current);
                }

                if (string.IsNullOrWhiteSpace(callerId))
                {
                    return current;
                }

                object parent = FindWorkflowInstanceById(callerId);
                if (parent == null)
                {
                    return current;
                }

                current = parent;
            }

            return current;
        }

        private static string FindCallerInstanceIdByBookmark(object childInstance)
        {
            if (childInstance == null)
            {
                return null;
            }

            string childDocId = GetStringPropertyValue(childInstance, "_id", "id", "Id");
            string childInstanceId = GetStringPropertyValue(childInstance, "InstanceId");
            if (string.IsNullOrWhiteSpace(childDocId) && string.IsNullOrWhiteSpace(childInstanceId))
            {
                return null;
            }

            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return null;
            }

            foreach (object parent in instances)
            {
                if (parent == null || ReferenceEquals(parent, childInstance))
                {
                    continue;
                }

                System.Collections.IEnumerable bookmarkKeys;
                if (!TryGetBookmarkKeys(parent, out bookmarkKeys))
                {
                    continue;
                }

                foreach (object keyObj in bookmarkKeys)
                {
                    string key = keyObj as string ?? (keyObj == null ? null : keyObj.ToString());
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (string.Equals(key, childDocId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, childInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return GetStringPropertyValue(parent, "InstanceId");
                    }
                }
            }

            return null;
        }

        private static bool TryGetBookmarkKeys(object instance, out System.Collections.IEnumerable keys)
        {
            keys = null;
            if (instance == null)
            {
                return false;
            }

            object bookmarks = GetPropertyValue(instance, "Bookmarks", "bookmarks");
            if (bookmarks == null)
            {
                return false;
            }

            if (bookmarks is System.Collections.IDictionary dictionary)
            {
                keys = dictionary.Keys;
                return true;
            }

            PropertyInfo keysProp = bookmarks.GetType().GetProperty(
                "Keys",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (keysProp != null)
            {
                keys = keysProp.GetValue(bookmarks) as System.Collections.IEnumerable;
                return keys != null;
            }

            return false;
        }

        private static object FindCurrentWorkflowInstance()
        {
            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return null;
            }

            object fallback = null;
            foreach (object item in instances)
            {
                if (item == null)
                {
                    continue;
                }

                bool? completed = GetBoolPropertyValue(item, "isCompleted");
                string state = GetStringPropertyValue(item, "state");

                if (completed == false)
                {
                    if (string.Equals(state, "running", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(state, "idle", StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }

                    if (fallback == null)
                    {
                        fallback = item;
                    }
                }
            }

            return fallback;
        }

        private static object FindWorkflowInstanceById(string workflowInstanceId)
        {
            if (string.IsNullOrWhiteSpace(workflowInstanceId))
            {
                return null;
            }

            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return null;
            }

            foreach (object item in instances)
            {
                if (item == null)
                {
                    continue;
                }

                string id = GetStringPropertyValue(item, "InstanceId");
                if (string.Equals(id, workflowInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }

                string docId = GetStringPropertyValue(item, "_id", "id", "Id");
                if (string.Equals(docId, workflowInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static System.Collections.IEnumerable GetWorkflowInstances()
        {
            try
            {
                Type wfType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                PropertyInfo instancesProp = wfType?.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                return instancesProp?.GetValue(null) as System.Collections.IEnumerable;
            }
            catch
            {
                return null;
            }
        }

        private static object GetPropertyValue(object target, params string[] propertyNames)
        {
            if (target == null || propertyNames == null)
            {
                return null;
            }

            Type type = target.GetType();
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = type.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    continue;
                }

                try
                {
                    return property.GetValue(target);
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool? GetBoolPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            object value = property.GetValue(target);
            return value is bool ? (bool?)value : null;
        }

        private static string GetStringPropertyValue(object target, params string[] propertyNames)
        {
            if (target == null || propertyNames == null || propertyNames.Length == 0)
            {
                return null;
            }

            Type type = target.GetType();
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = type.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    continue;
                }

                string text = property.GetValue(target) as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return null;
        }
    }
}
