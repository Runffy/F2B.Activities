using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    /// <summary>
    /// Organization document / sensitivity label applied before Save / Save As.
    /// Matched by display name against Word Sensitivity Labels when available;
    /// otherwise written to a custom document property as fallback.
    /// </summary>
    public enum WordDocumentLabel
    {
        [Description("(None)")]
        None = 0,

        [Description("Public")]
        Public = 1,

        [Description("Internal")]
        Internal = 2,

        [Description("Confidential")]
        Confidential = 3,

        [Description("Restricted")]
        Restricted = 4
    }
}
