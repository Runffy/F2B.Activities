namespace FlaUI.Inspector.Models
{
    public enum ValidationState
    {
        /// <summary>应用刚打开，尚未 Indicate 任何元素。</summary>
        None,

        /// <summary>已 Indicate 过，但 selector 已被编辑，等待用户手动 Validate。</summary>
        Stale,

        Valid,
        Ambiguous,
        Invalid
    }
}
