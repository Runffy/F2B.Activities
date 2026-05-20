using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using F2B.Browser.IExplore;
using F2B.Browser.IExplore.Com;

namespace IExplore.ComHost
{
    internal static class HostDomService
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue
        };

        public static void Execute(int hwnd, string requestPath, string responsePath)
        {
            var request = Serializer.Deserialize<IeComHostDomRequest>(File.ReadAllText(requestPath));
            var response = Execute(hwnd, request);
            File.WriteAllText(responsePath, Serializer.Serialize(response));
        }

        public static IeComHostDomResponse Execute(int hwnd, IeComHostDomRequest request)
        {
            try
            {
                var window = ResolveWindow(hwnd);
                return Execute(window, request);
            }
            catch (Exception ex)
            {
                return new IeComHostDomResponse { Ok = false, Error = ex.Message };
            }
        }

        private static IeComHostDomResponse Execute(HostDomContext window, IeComHostDomRequest request)
        {
            var op = (request.Op ?? string.Empty).Trim();
            var timeout = request.Timeout > 0 ? request.Timeout : OperationDefaults.TimeoutMs;

            switch (op)
            {
                case "navigate":
                    window.NavigateLocal(request.Url);
                    return Ok();

                case "html":
                    return OkString(window.Html);

                case "url":
                    return OkString(window.Url);

                case "script":
                {
                    var framePath = IEJsonParse.ParseFramePath(request.FramePathJson);
                    var doc = framePath == null || framePath.Count == 0
                        ? window.GetMsHtmlDocument()
                        : HtmlElementActions.ResolveDocument(window, framePath, timeout);

                    object rawElement = null;
                    if (!string.IsNullOrWhiteSpace(request.TargetElementJson))
                    {
                        var findReq = new IeComHostDomRequest
                        {
                            ElementJson = request.TargetElementJson,
                            FramePathJson = request.FramePathJson,
                            ScopeElementJson = request.ScopeElementJson,
                            ScopeFramePathJson = request.ScopeFramePathJson,
                            ScopeElementIdx = request.ScopeElementIdx
                        };
                        rawElement = IEHtmlElement.Unwrap(
                            HostDomRequestResolver.FindElement(window, findReq, timeout));
                    }

                    var args = IeScriptExecute.ParseArgsJson(request.ArgsJson);
                    var result = IeScriptExecute.Execute(doc, request.Script, rawElement, args);
                    return new IeComHostDomResponse { Ok = true, Result = result };
                }

                case "exists":
                    return new IeComHostDomResponse
                    {
                        Ok = true,
                        Found = HostDomRequestResolver.ElementExists(window, request)
                    };

                case "find":
                    HostDomRequestResolver.FindElement(window, request, timeout);
                    return new IeComHostDomResponse { Ok = true, Found = true };

                case "findelements":
                {
                    var elems = HostDomRequestResolver.FindElements(window, request, timeout);
                    return new IeComHostDomResponse { Ok = true, IntResult = elems.Length };
                }

                case "parallelfind":
                {
                    var result = HtmlElementActions.ParallelFindElement(
                        window,
                        ParseParallelLocators(request.LocatorsJson, request.FramePathJson),
                        timeout);
                    return new IeComHostDomResponse { Ok = true, ParallelIndex = result.Index };
                }

                case "click":
                    if (HostDomRequestResolver.HasScope(request))
                    {
                        var clickTarget = HostDomRequestResolver.FindElement(window, request, timeout);
                        var clickOptions = ElementLocatorOptions.Parse(
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            forInput: false);
                        HtmlElementActions.Click(
                            window,
                            clickTarget,
                            clickOptions.Button,
                            clickOptions.Mode,
                            timeout);
                    }
                    else
                    {
                        HtmlElementActions.Click(
                            window,
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            IEJsonParse.ParseFramePath(request.FramePathJson),
                            timeout);
                    }

                    return Ok();

                case "dblclick":
                    if (HostDomRequestResolver.HasScope(request))
                    {
                        var dblTarget = HostDomRequestResolver.FindElement(window, request, timeout);
                        var dblOptions = ElementLocatorOptions.Parse(
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            forInput: false);
                        HtmlElementActions.DoubleClick(
                            window,
                            dblTarget,
                            dblOptions.Button,
                            dblOptions.Mode,
                            dblOptions.ClickIntervalMs,
                            timeout);
                    }
                    else
                    {
                        HtmlElementActions.DoubleClick(
                            window,
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            IEJsonParse.ParseFramePath(request.FramePathJson),
                            timeout);
                    }

                    return Ok();

                case "input":
                {
                    var elementDict = IEJsonParse.ParseDictionary(request.ElementJson);
                    if (request.Value != null)
                        elementDict[ElementLocatorKeys.InputText] = request.Value;
                    if (HostDomRequestResolver.HasScope(request))
                    {
                        var target = HostDomRequestResolver.FindElement(window, request, timeout);
                        var text = request.Value
                            ?? ElementLocatorOptions.Parse(elementDict, forInput: true).Value;
                        HtmlElementActions.Input(window, target, text, timeout);
                    }
                    else
                    {
                        HtmlElementActions.Input(
                            window,
                            elementDict,
                            IEJsonParse.ParseFramePath(request.FramePathJson),
                            timeout);
                    }

                    return Ok();
                }

                case "check":
                    if (HostDomRequestResolver.HasScope(request))
                    {
                        HtmlElementActions.Check(
                            window,
                            HostDomRequestResolver.FindElement(window, request, timeout),
                            timeout);
                    }
                    else
                    {
                        HtmlElementActions.Check(
                            window,
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            IEJsonParse.ParseFramePath(request.FramePathJson),
                            timeout);
                    }

                    return Ok();

                case "uncheck":
                    if (HostDomRequestResolver.HasScope(request))
                    {
                        HtmlElementActions.Uncheck(
                            window,
                            HostDomRequestResolver.FindElement(window, request, timeout),
                            timeout);
                    }
                    else
                    {
                        HtmlElementActions.Uncheck(
                            window,
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            IEJsonParse.ParseFramePath(request.FramePathJson),
                            timeout);
                    }

                    return Ok();

                case "ischecked":
                    return new IeComHostDomResponse
                    {
                        Ok = true,
                        BoolResult = HostDomRequestResolver.HasScope(request)
                            ? HtmlElementActions.IsChecked(
                                window,
                                HostDomRequestResolver.FindElement(window, request, timeout),
                                timeout)
                            : HtmlElementActions.IsChecked(
                                window,
                                IEJsonParse.ParseDictionary(request.ElementJson),
                                IEJsonParse.ParseFramePath(request.FramePathJson),
                                timeout)
                    };

                case "select":
                    if (HostDomRequestResolver.HasScope(request))
                    {
                        var selectTarget = HostDomRequestResolver.FindElement(window, request, timeout);
                        var parsed = ElementLocatorParse.Parse(
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            LocatorOperation.Select);
                        HtmlElementDomHelper.SelectOptions(IEHtmlElement.Unwrap(selectTarget), parsed.SelectCriteria);
                    }
                    else
                    {
                        HtmlElementActions.Select(
                            window,
                            IEJsonParse.ParseDictionary(request.ElementJson),
                            IEJsonParse.ParseFramePath(request.FramePathJson),
                            timeout);
                    }

                    return Ok();

                case "gettext":
                    return OkString(HtmlElementActions.GetText(
                        window,
                        HostDomRequestResolver.FindElement(window, request, timeout),
                        timeout));

                case "getvalue":
                    return OkString(HtmlElementActions.GetValue(
                        window,
                        HostDomRequestResolver.FindElement(window, request, timeout),
                        timeout));

                case "getattribute":
                    return OkString(HtmlElementActions.GetAttribute(
                        window,
                        HostDomRequestResolver.FindElement(window, request, timeout),
                        request.AttributeName,
                        timeout));

                case "setattribute":
                    HtmlElementActions.SetAttribute(
                        window,
                        HostDomRequestResolver.FindElement(window, request, timeout),
                        request.AttributeName,
                        request.Value ?? string.Empty,
                        timeout);
                    return Ok();

                case "refresh":
                    window.Refresh(timeout);
                    return Ok();

                case "waitframe":
                    window.WaitForFrame(ParseLocator(request.ElementJson, request.FramePathJson), timeout);
                    return Ok();

                default:
                    return new IeComHostDomResponse { Ok = false, Error = "Unknown dom op: " + op };
            }
        }

        private static HostDomContext ResolveWindow(int hwnd)
        {
            var handle = new IntPtr(hwnd);
            var ieServer = HtmlDocumentHelper.FindInternetExplorerServer(handle);
            if (ieServer == IntPtr.Zero)
                throw new InvalidOperationException("Internet Explorer_Server not found for hwnd=" + hwnd);

            return new HostDomContext(handle, ieServer, IeHostWindow.IeFrameClass);
        }

        private static IELocator[] ParseParallelLocators(string locatorsJson, string framePathJson)
        {
            if (string.IsNullOrWhiteSpace(locatorsJson))
                throw new ArgumentException("LocatorsJson is required for parallelfind.", nameof(locatorsJson));

            var raw = Serializer.DeserializeObject(locatorsJson);
            var list = raw as IList;
            if (list == null || list.Count == 0)
                throw new ArgumentException("LocatorsJson must be a non-empty JSON array.", nameof(locatorsJson));

            var locators = new IELocator[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var element = list[i]?.ToString();
                if (string.IsNullOrWhiteSpace(element))
                    throw new ArgumentException("Locator at index " + i + " is empty.", nameof(locatorsJson));
                locators[i] = new IELocator(element, framePathJson);
            }

            return locators;
        }

        private static IELocator ParseLocator(string elementJson, string framePathJson) =>
            new IELocator(elementJson ?? "{'tag':'html'}", framePathJson);

        private static IELocator ParseLocatorOrNull(string elementJson, string framePathJson)
        {
            if (string.IsNullOrWhiteSpace(elementJson))
                return null;
            return ParseLocator(elementJson, framePathJson);
        }

        private static IeComHostDomResponse Ok() => new IeComHostDomResponse { Ok = true };

        private static IeComHostDomResponse OkString(string value) =>
            new IeComHostDomResponse { Ok = true, StringResult = value };
    }
}
