using System;
using System.Activities;
using System.Activities.Hosting;
using System.Activities.Tracking;
using System.Collections.Generic;

namespace F2B.Basic
{
    /// <summary>
    /// Records the first Faulted activity Id in a fault wave (leaf faults before parents).
    /// Also keeps the last Executing activity Id as a fallback.
    /// </summary>
    internal sealed class FaultPathTrackingExtension : IWorkflowInstanceExtension
    {
        private readonly object _sync = new object();

        public string FirstFaultActivityId { get; private set; }
        public string LastExecutingActivityId { get; private set; }

        public void Reset()
        {
            lock (_sync)
            {
                FirstFaultActivityId = null;
                LastExecutingActivityId = null;
            }
        }

        public void OnExecuting(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
            {
                return;
            }

            lock (_sync)
            {
                LastExecutingActivityId = activityId;
            }
        }

        public void OnFaulted(string activityId)
        {
            if (string.IsNullOrEmpty(activityId))
            {
                return;
            }

            lock (_sync)
            {
                // Leaf faults first while bubbling; keep the first Id only.
                if (string.IsNullOrEmpty(FirstFaultActivityId))
                {
                    FirstFaultActivityId = activityId;
                }
            }
        }

        public string ResolveFaultActivityId(string tryRootActivityId = null)
        {
            lock (_sync)
            {
                // Prefer first Faulted that is not the Try root itself (leaf faults first).
                if (!string.IsNullOrEmpty(FirstFaultActivityId) &&
                    !string.Equals(FirstFaultActivityId, tryRootActivityId, StringComparison.Ordinal))
                {
                    return FirstFaultActivityId;
                }

                if (!string.IsNullOrEmpty(LastExecutingActivityId) &&
                    !string.Equals(LastExecutingActivityId, tryRootActivityId, StringComparison.Ordinal))
                {
                    return LastExecutingActivityId;
                }

                if (!string.IsNullOrEmpty(FirstFaultActivityId))
                {
                    return FirstFaultActivityId;
                }

                return LastExecutingActivityId;
            }
        }

        public IEnumerable<object> GetAdditionalExtensions()
        {
            yield return new FaultPathTrackingParticipant(this);
        }

        public void SetInstance(WorkflowInstanceProxy instance)
        {
        }
    }

    internal sealed class FaultPathTrackingParticipant : TrackingParticipant
    {
        private readonly FaultPathTrackingExtension _owner;

        public FaultPathTrackingParticipant(FaultPathTrackingExtension owner)
        {
            _owner = owner;
            TrackingProfile = new TrackingProfile
            {
                Name = "F2B.FaultPathTracking",
                Queries =
                {
                    new ActivityStateQuery
                    {
                        ActivityName = "*",
                        States = { ActivityStates.Executing, ActivityStates.Faulted }
                    }
                }
            };
        }

        protected override void Track(TrackingRecord record, TimeSpan timeout)
        {
            var stateRecord = record as ActivityStateRecord;
            if (stateRecord == null || stateRecord.Activity == null)
            {
                return;
            }

            string activityId = stateRecord.Activity.Id;
            if (string.IsNullOrEmpty(activityId))
            {
                return;
            }

            if (string.Equals(stateRecord.State, ActivityStates.Executing, StringComparison.OrdinalIgnoreCase))
            {
                _owner.OnExecuting(activityId);
            }
            else if (string.Equals(stateRecord.State, ActivityStates.Faulted, StringComparison.OrdinalIgnoreCase))
            {
                _owner.OnFaulted(activityId);
            }
        }
    }
}
