using System;
using System.Activities.Hosting;
using System.Collections.Generic;

namespace F2B.Forms.Session
{
    /// <summary>
    /// Allows UI thread to resume workflow bookmarks without referencing OpenRPA.
    /// </summary>
    public sealed class WorkflowInstanceExtension : IWorkflowInstanceExtension
    {
        private WorkflowInstanceProxy _proxy;

        public IEnumerable<object> GetAdditionalExtensions()
        {
            return null;
        }

        public void SetInstance(WorkflowInstanceProxy instance)
        {
            _proxy = instance;
        }

        public void BeginResumeBookmark(string bookmarkName, object value)
        {
            if (_proxy == null)
            {
                throw new InvalidOperationException("WorkflowInstanceProxy is not available.");
            }

            _proxy.BeginResumeBookmark(new System.Activities.Bookmark(bookmarkName), value, null, null);
        }

        public bool TryBeginResumeBookmark(string bookmarkName, object value)
        {
            if (_proxy == null)
            {
                return false;
            }

            try
            {
                _proxy.BeginResumeBookmark(new System.Activities.Bookmark(bookmarkName), value, null, null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
