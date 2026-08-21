using System.Collections.Generic;
using Newtonsoft.Json;

namespace F2B.Forms.Model
{
    public sealed class FormDefinition
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";

        [JsonProperty("id")]
        public string Id { get; set; } = "form";

        [JsonProperty("title")]
        public string Title { get; set; } = "Form";

        [JsonProperty("width")]
        public int Width { get; set; } = 640;

        [JsonProperty("height")]
        public int Height { get; set; } = 480;

        /// <summary>
        /// When false, runtime form cannot be resized (edge drag / maximize).
        /// </summary>
        [JsonProperty("allowResize")]
        public bool AllowResize { get; set; } = true;

        [JsonProperty("startPosition")]
        public string StartPosition { get; set; } = "CenterScreen";

        /// <summary>
        /// Optional culture for form UI thread (e.g. en-US, zh-CN).
        /// Controls DateTimePicker / DatePicker calendar language. Empty = Windows display language.
        /// </summary>
        [JsonProperty("culture")]
        public string Culture { get; set; }

        [JsonProperty("controls")]
        public List<ControlDefinition> Controls { get; set; } = new List<ControlDefinition>();
    }
}
