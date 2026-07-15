using System.ComponentModel;

namespace F2B.Microsoft.Word
{
    public enum WordImageUnit
    {
        [Description("cm")]
        Cm = 0,

        [Description("mm")]
        Mm = 1,

        [Description("inch")]
        Inch = 2,

        [Description("px")]
        Px = 3,

        [Description("pt")]
        Pt = 4
    }
}
