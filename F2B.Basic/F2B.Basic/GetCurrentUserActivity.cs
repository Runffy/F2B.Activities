using System;
using System.Activities;
using System.ComponentModel;
using System.DirectoryServices;
using System.Text;
using System.Windows;

namespace F2B.Basic
{
    [ToolboxPath("System")]
    [DisplayName("Get Current User")]
    [Description("Returns the current Windows user name and domain Full Name (displayName via LDAP, same as net user /domain).")]
    public sealed class GetCurrentUserActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetCurrentUserActivity()
        {
            DisplayName = "Get Current User";
        }

        [DisplayName("User Name")]
        [Description("Current Windows login name (Environment.UserName).")]
        [Category("Output")]
        public OutArgument<string> UserName { get; set; }

        [DisplayName("Full Name")]
        [Description("Domain display name (AD displayName). Empty when LDAP lookup fails or is unavailable.")]
        [Category("Output")]
        public OutArgument<string> FullName { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetCurrentUserActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string userName = Environment.UserName ?? string.Empty;
            string fullName = TryGetDomainFullName(userName) ?? string.Empty;

            UserName?.Set(context, userName);
            FullName?.Set(context, fullName);
        }

        private static string TryGetDomainFullName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            try
            {
                using (var rootDse = new DirectoryEntry("LDAP://RootDSE"))
                {
                    object namingContext = rootDse.Properties["defaultNamingContext"]?.Value;
                    if (namingContext == null)
                    {
                        return null;
                    }

                    using (var searchRoot = new DirectoryEntry("LDAP://" + namingContext))
                    using (var searcher = new DirectorySearcher(searchRoot))
                    {
                        searcher.Filter = "(sAMAccountName=" + EscapeLdapFilterValue(userName.Trim()) + ")";
                        searcher.PropertiesToLoad.Add("displayName");
                        searcher.SizeLimit = 1;

                        SearchResult result = searcher.FindOne();
                        if (result == null || !result.Properties.Contains("displayName") ||
                            result.Properties["displayName"].Count == 0)
                        {
                            return null;
                        }

                        return result.Properties["displayName"][0]?.ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Escapes LDAP filter special characters per RFC 4515.</summary>
        private static string EscapeLdapFilterValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append(@"\5c");
                        break;
                    case '*':
                        sb.Append(@"\2a");
                        break;
                    case '(':
                        sb.Append(@"\28");
                        break;
                    case ')':
                        sb.Append(@"\29");
                        break;
                    case '\0':
                        sb.Append(@"\00");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
