using System.Activities;
using System.ComponentModel;

namespace F2B.Basic
{
    /// <summary>
    /// One assign pair for <see cref="MultipleAssignActivity"/>: left-value To and right-value Value.
    /// </summary>
    public sealed class AssignEntry
    {
        [DisplayName("To")]
        [Description("Target variable or location (L-value).")]
        public OutArgument<object> To { get; set; }

        [DisplayName("Value")]
        [Description("Value expression assigned to To.")]
        public InArgument<object> Value { get; set; }
    }
}
