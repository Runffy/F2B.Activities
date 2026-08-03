namespace F2B.Browser.Chromium.Cdp.Browser
{
    public enum CdpMouseButton
    {
        Left,
        Middle,
        Right
    }

    public enum CdpInteractionMethod
    {
        Simulate,
        Js
    }

    public enum CdpScrollDirection
    {
        Down,
        Up,
        Left,
        Right
    }

    public enum CdpLocalFileExistsAction
    {
        AutoRename,
        Overwrite,
        Skip
    }

    public enum CdpSelectBy
    {
        Text,
        Value,
        Index
    }

    /// <summary>
    /// Scope for <see cref="CdpTab.WaitForDocumentComplete"/>.
    /// </summary>
    public enum CdpDocumentWaitScope
    {
        /// <summary>Wait for the main frame document only.</summary>
        MainDocument,

        /// <summary>Wait for the main frame and every child frame in the frame tree.</summary>
        AllDocuments
    }
}
