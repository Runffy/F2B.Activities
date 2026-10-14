using System;
using System.Activities;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpListenActivityHelper
    {
        public static CdpNetworkListener GetListener(InArgument<CdpBase> targetArgument, ActivityContext context)
        {
            if (targetArgument == null)
            {
                throw new InvalidOperationException("Target is required (CdpTab or CdpFrame).");
            }

            var target = targetArgument.Get(context);
            if (target == null)
            {
                throw new InvalidOperationException("Target is required (CdpTab or CdpFrame).");
            }

            return CdpNetworkListener.From(target);
        }
    }
}
