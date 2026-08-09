namespace F2B.Forms.Session
{
    public enum FormCloseReason
    {
        None = 0,
        UserClose = 1,
        CloseForm = 2,
        Timeout = 3,
        Error = 4,
        WorkflowAbort = 5
    }

    public enum UiBehavior
    {
        /// <summary>Disable UI while handler runs; queue new events.</summary>
        LockQueue = 0,
        /// <summary>Disable UI while handler runs; ignore new events.</summary>
        LockIgnore = 1,
        /// <summary>Do not disable UI; queue events.</summary>
        NoLock = 2
    }

    public sealed class FormEvent
    {
        public string ControlId { get; set; }
        public string EventName { get; set; }
        public object EventArgs { get; set; }
    }
}
