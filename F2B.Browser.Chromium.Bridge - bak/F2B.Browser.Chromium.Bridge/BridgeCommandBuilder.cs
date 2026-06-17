namespace F2B.Browser.Chromium.Bridge
{
    public static class BridgeCommandBuilder
    {
        public static string Alert(string message)
        {
            var escaped = EscapeJson(message ?? string.Empty);
            return "{\"type\":\"command\",\"action\":\"alert\",\"message\":\"" + escaped + "\"}";
        }

        private static string EscapeJson(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
