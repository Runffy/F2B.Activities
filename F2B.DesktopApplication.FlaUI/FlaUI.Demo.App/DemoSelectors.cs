namespace FlaUI.Demo.App
{
    /// <summary>
    /// Inspector-compatible selector snippets for OpenRPA test workflow.
    /// </summary>
    public static class DemoSelectors
    {
        public const string Window =
            "<wnd role='window' title='FlaUI Demo App' app='FlaUI.Demo.App' />";

        public const string BtnClick =
            Window + "\r\n<ctrl role='button' automationid='btnClick' name='Click Me' />";

        public const string BtnClickScoped =
            "<ctrl role='button' automationid='btnClick' name='Click Me' />";

        public const string BtnDoubleClick =
            Window + "\r\n<ctrl role='button' automationid='btnDoubleClick' name='Double Click Me' />";

        public const string ChkAgree =
            Window + "\r\n<ctrl role='check box' automationid='chkAgree' name='I agree to the terms' />";

        public const string TxtName =
            Window + "\r\n<ctrl role='edit' automationid='txtName' name='Name' />";

        public const string CboCity =
            Window + "\r\n<ctrl role='combo box' automationid='cboCity' name='City' />";

        public const string BtnDisabled =
            Window + "\r\n<ctrl role='button' automationid='btnDisabled' name='Disabled Button' />";

        public const string LblStatus =
            Window + "\r\n<ctrl role='text' automationid='lblStatus' name='Status' />";

        public const string BtnScrollTarget =
            Window + "\r\n<ctrl role='button' automationid='btnScrollTarget' name='Scroll Target Button' />";

        public const string ScrollMain =
            Window + "\r\n<ctrl role='pane' automationid='scrollMain' />";
    }
}
