using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace F2B.Browser.IExplore.Com
{
    internal static class RemoteElementRefJson
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static string WithIndex(string elementJson, int? elementIdx)
        {
            if (!elementIdx.HasValue)
                return elementJson;

            var dict = IEJsonParse.ParseDictionary(elementJson);
            dict[ElementLocatorKeys.Idx] = elementIdx.Value.ToString();
            return Serializer.Serialize(dict);
        }
    }
}
