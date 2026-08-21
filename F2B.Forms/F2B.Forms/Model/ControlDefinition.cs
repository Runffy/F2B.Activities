using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace F2B.Forms.Model
{
    public sealed class ControlDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; } = 100;

        [JsonProperty("height")]
        public int Height { get; set; } = 23;

        [JsonProperty("enabled")]
        public bool? Enabled { get; set; }

        [JsonProperty("visible")]
        public bool? Visible { get; set; }

        /// <summary>
        /// DatePicker / DateTimePicker calendar language (e.g. en-US, zh-CN). Empty = form Culture or Windows.
        /// </summary>
        [JsonProperty("culture")]
        public string Culture { get; set; }

        [JsonProperty("readOnly")]
        public bool? ReadOnly { get; set; }

        [JsonProperty("checked")]
        public bool? Checked { get; set; }

        [JsonProperty("items")]
        public List<string> Items { get; set; }

        [JsonProperty("selectedIndex")]
        public int? SelectedIndex { get; set; }

        [JsonProperty("maxLength")]
        public int? MaxLength { get; set; }

        /// <summary>MaskedTextBox input mask, e.g. 000-000-0000. Not for password masking.</summary>
        [JsonProperty("mask")]
        public string Mask { get; set; }

        /// <summary>
        /// TextBox password mask character, e.g. "*". Empty / null = normal text.
        /// When set, the TextBox is forced to single-line (WinForms ignores PasswordChar on multiline).
        /// </summary>
        [JsonProperty("passwordChar")]
        public string PasswordChar { get; set; }

        /// <summary>NumericUpDown minimum.</summary>
        [JsonProperty("minimum")]
        public decimal? Minimum { get; set; }

        /// <summary>NumericUpDown maximum.</summary>
        [JsonProperty("maximum")]
        public decimal? Maximum { get; set; }

        /// <summary>NumericUpDown step.</summary>
        [JsonProperty("increment")]
        public decimal? Increment { get; set; }

        /// <summary>NumericUpDown decimal places.</summary>
        [JsonProperty("decimalPlaces")]
        public int? DecimalPlaces { get; set; }

        /// <summary>PictureBox image file path.</summary>
        [JsonProperty("imagePath")]
        public string ImagePath { get; set; }

        /// <summary>PictureBox SizeMode: Normal | StretchImage | Zoom | CenterImage | AutoSize.</summary>
        [JsonProperty("sizeMode")]
        public string SizeMode { get; set; }

        [JsonProperty("scrollBars")]
        public string ScrollBars { get; set; }

        /// <summary>
        /// TextArea only. true (default) wraps lines; false keeps long lines and needs Horizontal/Both scroll bars.
        /// </summary>
        [JsonProperty("wordWrap")]
        public bool? WordWrap { get; set; }

        [JsonProperty("anchor")]
        public string Anchor { get; set; }

        /// <summary>Left | Center | Right</summary>
        [JsonProperty("textAlignH")]
        public string TextAlignH { get; set; }

        /// <summary>Top | Middle | Bottom</summary>
        [JsonProperty("textAlignV")]
        public string TextAlignV { get; set; }

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

        /// <summary>HTML color, e.g. #000000 or Red.</summary>
        [JsonProperty("foreColor")]
        public string ForeColor { get; set; }

        /// <summary>HTML color, e.g. #FFFFFF. Null = system default.</summary>
        [JsonProperty("backColor")]
        public string BackColor { get; set; }

        /// <summary>TableLayout: number of rows.</summary>
        [JsonProperty("rowCount")]
        public int? RowCount { get; set; }

        /// <summary>TableLayout: number of columns.</summary>
        [JsonProperty("columnCount")]
        public int? ColumnCount { get; set; }

        /// <summary>Child of TableLayout: zero-based row index.</summary>
        [JsonProperty("row")]
        public int? Row { get; set; }

        /// <summary>Child of TableLayout: zero-based column index.</summary>
        [JsonProperty("column")]
        public int? Column { get; set; }

        [JsonProperty("controls")]
        public List<ControlDefinition> Controls { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }
}
