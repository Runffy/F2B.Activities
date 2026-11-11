using System.Collections.Generic;
using Newtonsoft.Json;

namespace F2B.Forms.Model
{
    /// <summary>
    /// One MixTable column. type must match Designer names exactly:
    /// Label | TextBox | ComboBox | CheckBox | Button | Link (case-sensitive).
    /// </summary>
    public sealed class MixTableColumnDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>Label | TextBox | ComboBox | CheckBox | Button | Link (case-sensitive).</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>Header text; for Button/Link also used as default cell text when value is empty.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }

        /// <summary>ComboBox items.</summary>
        [JsonProperty("items")]
        public List<string> Items { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("readOnly")]
        public bool? ReadOnly { get; set; }

        /// <summary>
        /// Header text align: left-top | middle-top | right-top | left-center | middle-center |
        /// right-center | left-bottom | middle-bottom | right-bottom (also accepts TopLeft etc.).
        /// </summary>
        [JsonProperty("headerAlign")]
        public string HeaderAlign { get; set; }

        /// <summary>Optional column default font family (cells in this column).</summary>
        [JsonProperty("fontFamily")]
        public string FontFamily { get; set; }

        [JsonProperty("fontSize")]
        public float? FontSize { get; set; }

        [JsonProperty("fontBold")]
        public bool? FontBold { get; set; }

        [JsonProperty("fontItalic")]
        public bool? FontItalic { get; set; }

        [JsonProperty("fontUnderline")]
        public bool? FontUnderline { get; set; }

        /// <summary>HTML / named color for column default fore color.</summary>
        [JsonProperty("foreColor")]
        public string ForeColor { get; set; }

        /// <summary>HTML / named color for column default back color.</summary>
        [JsonProperty("backColor")]
        public string BackColor { get; set; }
    }
}
