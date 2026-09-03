using System.Activities;
using System.ComponentModel;

namespace F2B.Basic
{
    /// <summary>
    /// One ElseIf branch for <see cref="ElseIfActivity"/>: condition + body.
    /// </summary>
    public sealed class ElseIfBranch
    {
        [DisplayName("Condition")]
        [Description("When true (and prior If/ElseIf conditions were false), Body is executed. Later conditions are not evaluated.")]
        public InArgument<bool> Condition { get; set; }

        [Browsable(false)]
        public Activity Body { get; set; }
    }
}
