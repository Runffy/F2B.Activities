using System.Activities;
using System.ComponentModel;

namespace F2B.Basic
{
    /// <summary>
    /// One assign pair for <see cref="MultipleAssignActivity"/>: left-value To and right-value Value.
    /// Uses non-generic <see cref="Argument"/> so each row can be String, Int32, etc.
    /// </summary>
    public sealed class AssignEntry
    {
        [DisplayName("To")]
        [Description("Target variable or location (L-value).")]
        public Argument To { get; set; }

        [DisplayName("Value")]
        [Description("Value expression assigned to To.")]
        public Argument Value { get; set; }
    }
}
