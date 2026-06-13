namespace Bridge.Demo.App
{
    public static class BridgeDemoSelectors
    {
        public const string BaiduSearchButton =
            "<wnd role='window' url-re='https://www\\.baidu\\.com' />\r\n" +
            "<ctrl role='button' automationid='chat-submit-button' />";

        public const string BaiduSearchInput =
            "<wnd role='window' url='https://www.baidu.com/' />\r\n" +
            "<ctrl role='edit' automationid='kw' name='百度一下，你就知道' />";
    }
}
