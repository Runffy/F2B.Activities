using System.Collections.Generic;

namespace F2B.Bridge.ConsoleTest
{
    internal static class DemoSelectors
    {
        public const string WndMain = "<wnd title-re='GWIS SYSTEM.*' />";
        public const string WndNavA = "<wnd title='GWIS Nav A' />";
        public const string WndNavB = "<wnd title='GWIS Nav B' />";

        public const string FrmLogin = "<frm name='LoginWinMain' />";
        public const string FrmInner = "<frm name='InnerPanel' />";

        public const string TopBtn = "<ctrl role='button' automationid='topBtn' />";
        public const string TopStatus = "<ctrl automationid='topStatus' />";
        public const string DblClickBtn = "<ctrl role='button' automationid='dblClickBtn' />";
        public const string DblClickStatus = "<ctrl automationid='dblClickStatus' />";
        public const string AgreeTerms = "<ctrl role='checkbox' automationid='agreeTerms' />";
        public const string TopCountry = "<ctrl role='combobox' automationid='topCountry' />";
        public const string TopCountryStatus = "<ctrl automationid='topCountryStatus' />";
        public const string PlanBasic = "<ctrl role='radiobutton' automationid='planBasic' />";
        public const string PlanPro = "<ctrl role='radiobutton' automationid='planPro' />";
        public const string TopNotes = "<ctrl role='edit' automationid='topNotes' />";
        public const string SendKeysTarget = "<ctrl role='edit' automationid='sendKeysTarget' />";
        public const string ChildTarget = "<ctrl automationid='childTarget' />";
        public const string ParentBox = "<ctrl automationid='parentBox' />";
        public const string ChildScoped = "<ctrl cls='demo-child' />";
        public const string RectBox = "<ctrl automationid='rectBox' />";
        public const string SetAttrBtn = "<ctrl role='button' automationid='setAttrBtn' />";
        public const string BtnCandidateA = "<ctrl role='button' automationid='btnCandidateA' />";
        public const string BtnCandidateB = "<ctrl role='button' automationid='btnCandidateB' />";
        public const string OpenNewTabLink = "<ctrl role='link' automationid='openNewTabLink' />";
        public const string DownloadLink = "<ctrl role='link' automationid='downloadLink' />";
        public const string GotoNavA = "<ctrl role='link' automationid='gotoNavA' />";

        public const string UserId = "<ctrl tag='input' name='userID' />";
        public const string Password = "<ctrl tag='input' name='password' />";
        public const string BtnLogin = "<ctrl role='button' automationid='btnLogin' />";
        public const string StatusLabel = "<ctrl automationid='statusLabel' />";
        public const string DeptSelect = "<ctrl role='combobox' automationid='deptSelect' />";
        public const string RememberMe = "<ctrl role='checkbox' automationid='rememberMe' />";
        public const string GenderM = "<ctrl role='radiobutton' automationid='genderM' />";
        public const string LoginNotes = "<ctrl role='edit' automationid='loginNotes' />";
        public const string InnerCode = "<ctrl tag='input' name='innerCode' />";

        public const string NavAHint = "<ctrl automationid='navAHint' />";
        public const string NavBHint = "<ctrl automationid='navBHint' />";
        public const string NotExists = "<ctrl automationid='notExistsElement' />";

        public static string WithWnd(params string[] levels)
        {
            var parts = new List<string> { WndMain };
            Append(parts, levels);
            return Join(parts);
        }

        public static string InLogin(params string[] ctrlLevels)
        {
            var parts = new List<string> { WndMain, FrmLogin };
            Append(parts, ctrlLevels);
            return Join(parts);
        }

        public static string InNested(params string[] ctrlLevels)
        {
            var parts = new List<string> { WndMain, FrmLogin, FrmInner };
            Append(parts, ctrlLevels);
            return Join(parts);
        }

        public static string TabScope(string frmLine, params string[] ctrlLevels)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(frmLine))
                parts.Add(frmLine);
            Append(parts, ctrlLevels);
            return Join(parts);
        }

        private static void Append(List<string> parts, string[] lines)
        {
            if (lines == null)
                return;

            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                    parts.Add(line);
            }
        }

        private static string Join(List<string> parts)
        {
            return string.Join("\r\n", parts);
        }
    }
}
