namespace F2B.Browser.Chromium.Bridge
{
    public enum BridgeInputMethod
    {
        Fill,
        Type
    }

    public enum BridgeClickValidateMode
    {
        None,
        ElementDisappear,
        ElementAppear
    }

    public enum BridgeFindElementWaitState
    {
        None,
        Visible,
        Attached,
        Hidden,
        Detached
    }

    public enum BridgeSelectValueType
    {
        Text,
        Value,
        Index
    }

    public enum BridgeStorageScope
    {
        Local,
        Session
    }

    public enum BridgeTabLoadStatus
    {
        Unknown,
        Loading,
        Complete,
        Unloaded
    }
}
