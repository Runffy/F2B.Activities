using System;
using System.ComponentModel;
using System.Globalization;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal sealed class CdpBooleanTypeConverter : BooleanConverter
    {
    }

    internal sealed class CdpElementTargetTypeConverter : EnumConverter
    {
        public CdpElementTargetTypeConverter()
            : base(typeof(CdpElementTargetType))
        {
        }
    }

    internal sealed class CdpBrowserWindowStateOptionConverter : EnumConverter
    {
        public CdpBrowserWindowStateOptionConverter()
            : base(typeof(CdpBrowserWindowStateOption))
        {
        }
    }

    internal sealed class CdpMouseButtonConverter : EnumConverter
    {
        public CdpMouseButtonConverter()
            : base(typeof(Browser.CdpMouseButton))
        {
        }
    }

    internal sealed class CdpInteractionMethodConverter : EnumConverter
    {
        public CdpInteractionMethodConverter()
            : base(typeof(Browser.CdpInteractionMethod))
        {
        }
    }

    internal sealed class CdpScrollDirectionConverter : EnumConverter
    {
        public CdpScrollDirectionConverter()
            : base(typeof(Browser.CdpScrollDirection))
        {
        }
    }

    internal sealed class CdpLocalFileExistsActionConverter : EnumConverter
    {
        public CdpLocalFileExistsActionConverter()
            : base(typeof(Browser.CdpLocalFileExistsAction))
        {
        }
    }

    internal sealed class CdpActivitySelectByConverter : EnumConverter
    {
        public CdpActivitySelectByConverter()
            : base(typeof(CdpActivitySelectBy))
        {
        }
    }

    internal sealed class CdpAttributeOperationTypeConverter : EnumConverter
    {
        public CdpAttributeOperationTypeConverter()
            : base(typeof(CdpAttributeOperationType))
        {
        }
    }

    internal sealed class CdpStyleOperationTypeConverter : EnumConverter
    {
        public CdpStyleOperationTypeConverter()
            : base(typeof(CdpStyleOperationType))
        {
        }
    }

    internal sealed class CdpPropertyOperationTypeConverter : EnumConverter
    {
        public CdpPropertyOperationTypeConverter()
            : base(typeof(CdpPropertyOperationType))
        {
        }
    }

    internal sealed class CdpClassOperationTypeConverter : EnumConverter
    {
        public CdpClassOperationTypeConverter()
            : base(typeof(CdpClassOperationType))
        {
        }
    }

    internal sealed class CdpElementTextTypeConverter : EnumConverter
    {
        public CdpElementTextTypeConverter()
            : base(typeof(CdpElementTextType))
        {
        }
    }

    internal sealed class CdpNavigateTypeConverter : EnumConverter
    {
        public CdpNavigateTypeConverter()
            : base(typeof(CdpNavigateType))
        {
        }
    }

    internal sealed class CdpWaitForStateConverter : EnumConverter
    {
        public CdpWaitForStateConverter()
            : base(typeof(CdpWaitForState))
        {
        }
    }

    internal sealed class CdpTakeScreenshotBaseOnConverter : EnumConverter
    {
        public CdpTakeScreenshotBaseOnConverter()
            : base(typeof(CdpTakeScreenshotBaseOn))
        {
        }
    }
}
