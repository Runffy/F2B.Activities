using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CommandLineParser
    {
        public static IEnumerable<string> ParseArguments(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                yield break;
            }

            var index = 0;
            while (index < commandLine.Length)
            {
                while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
                {
                    index++;
                }

                if (index >= commandLine.Length)
                {
                    yield break;
                }

                string token;
                if (commandLine[index] == '"')
                {
                    index++;
                    var start = index;
                    while (index < commandLine.Length && commandLine[index] != '"')
                    {
                        index++;
                    }

                    token = commandLine.Substring(start, index - start);
                    if (index < commandLine.Length)
                    {
                        index++;
                    }
                }
                else
                {
                    var start = index;
                    while (index < commandLine.Length && !char.IsWhiteSpace(commandLine[index]))
                    {
                        index++;
                    }

                    token = commandLine.Substring(start, index - start);
                }

                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                }
            }
        }

        public static IEnumerable<string> FilterReservedArguments(IEnumerable<string> arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument.StartsWith("--remote-debugging-port=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (argument.StartsWith("--user-data-dir", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return argument;
            }
        }
    }
}
