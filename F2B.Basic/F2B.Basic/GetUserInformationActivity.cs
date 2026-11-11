using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.Security.Principal;
using System.Text;
using System.Windows;

namespace F2B.Basic
{
    [ToolboxPath("System")]
    [DisplayName("Get User Information")]
    [Description("Returns user name, domain Full Name (displayName), token groups (whoami-style), and LDAP memberOf groups. Optional User Name looks up that account; leave empty for the current user.")]
    public sealed class GetUserInformationActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private static readonly string[] EmptyGroups = new string[0];

        public GetUserInformationActivity()
        {
            DisplayName = "Get User Information";
        }

        [DisplayName("User Name")]
        [Description("Optional sAMAccountName (also accepts DOMAIN\\user or user@domain). Empty = current Windows user.")]
        [Category("Input")]
        public InArgument<string> User { get; set; }

        [DisplayName("User Name")]
        [Description("Resolved Windows / sAMAccountName used for the lookup.")]
        [Category("Output")]
        public OutArgument<string> UserName { get; set; }

        [DisplayName("Full Name")]
        [Description("Domain display name (AD displayName). Empty when LDAP lookup fails or is unavailable.")]
        [Category("Output")]
        public OutArgument<string> FullName { get; set; }

        [DisplayName("AD Groups")]
        [Description("For current user: access-token groups (whoami /groups). For other users: AD tokenGroups (nested security groups). NTAccount names when resolvable, otherwise SIDs.")]
        [Category("Output")]
        public OutArgument<string[]> AdGroups { get; set; }

        [DisplayName("LDAP Groups")]
        [Description("Direct AD group memberships from LDAP memberOf (CN names). Empty array when LDAP lookup fails.")]
        [Category("Output")]
        public OutArgument<string[]> LdapGroups { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetUserInformationActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string requested = User == null ? null : User.Get(context);
            string userName = ResolveSamAccountName(requested);
            bool isCurrentUser = IsCurrentUser(requested, userName);

            DomainUserInfo ldapInfo = TryGetDomainUserInfo(userName, loadTokenGroups: !isCurrentUser);
            string[] adGroups = isCurrentUser
                ? GetCurrentTokenGroups()
                : (ldapInfo.TokenGroups ?? EmptyGroups);

            UserName?.Set(context, userName);
            FullName?.Set(context, ldapInfo.FullName ?? string.Empty);
            AdGroups?.Set(context, adGroups);
            LdapGroups?.Set(context, ldapInfo.LdapGroups ?? EmptyGroups);
        }

        private static string ResolveSamAccountName(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                return Environment.UserName ?? string.Empty;
            }

            string trimmed = requested.Trim();
            int slash = trimmed.IndexOf('\\');
            if (slash >= 0 && slash < trimmed.Length - 1)
            {
                return trimmed.Substring(slash + 1);
            }

            int at = trimmed.IndexOf('@');
            if (at > 0)
            {
                return trimmed.Substring(0, at);
            }

            return trimmed;
        }

        private static bool IsCurrentUser(string requested, string resolvedSam)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                return true;
            }

            string current = Environment.UserName ?? string.Empty;
            return string.Equals(resolvedSam, current, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Same membership set as <c>whoami /groups</c> for the current process token.
        /// </summary>
        private static string[] GetCurrentTokenGroups()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    IdentityReferenceCollection groups = identity?.Groups;
                    if (groups == null || groups.Count == 0)
                    {
                        return EmptyGroups;
                    }

                    var names = new List<string>(groups.Count);
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (IdentityReference group in groups)
                    {
                        string name = TryTranslateSidName(group);
                        if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                        {
                            continue;
                        }

                        names.Add(name);
                    }

                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    return names.ToArray();
                }
            }
            catch
            {
                return EmptyGroups;
            }
        }

        private static string TryTranslateSidName(IdentityReference identity)
        {
            if (identity == null)
            {
                return null;
            }

            try
            {
                return identity.Translate(typeof(NTAccount)).Value;
            }
            catch
            {
                return identity.Value;
            }
        }

        private static DomainUserInfo TryGetDomainUserInfo(string userName, bool loadTokenGroups)
        {
            var info = new DomainUserInfo
            {
                FullName = string.Empty,
                LdapGroups = EmptyGroups,
                TokenGroups = EmptyGroups
            };

            if (string.IsNullOrWhiteSpace(userName))
            {
                return info;
            }

            try
            {
                using (var rootDse = new DirectoryEntry("LDAP://RootDSE"))
                {
                    object namingContext = rootDse.Properties["defaultNamingContext"]?.Value;
                    if (namingContext == null)
                    {
                        return info;
                    }

                    using (var searchRoot = new DirectoryEntry("LDAP://" + namingContext))
                    using (var searcher = new DirectorySearcher(searchRoot))
                    {
                        searcher.Filter = "(sAMAccountName=" + EscapeLdapFilterValue(userName.Trim()) + ")";
                        searcher.PropertiesToLoad.Add("displayName");
                        searcher.PropertiesToLoad.Add("memberOf");
                        if (loadTokenGroups)
                        {
                            searcher.PropertiesToLoad.Add("tokenGroups");
                        }

                        searcher.SizeLimit = 1;

                        SearchResult result = searcher.FindOne();
                        if (result == null)
                        {
                            return info;
                        }

                        if (result.Properties.Contains("displayName") && result.Properties["displayName"].Count > 0)
                        {
                            info.FullName = result.Properties["displayName"][0]?.ToString() ?? string.Empty;
                        }

                        info.LdapGroups = ReadMemberOfGroupNames(result);
                        if (loadTokenGroups)
                        {
                            info.TokenGroups = ReadTokenGroups(result);
                        }

                        return info;
                    }
                }
            }
            catch
            {
                return info;
            }
        }

        private static string[] ReadMemberOfGroupNames(SearchResult result)
        {
            if (result == null || !result.Properties.Contains("memberOf"))
            {
                return EmptyGroups;
            }

            ResultPropertyValueCollection values = result.Properties["memberOf"];
            if (values == null || values.Count == 0)
            {
                return EmptyGroups;
            }

            var names = new List<string>(values.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object value in values)
            {
                string cn = GetCnFromDn(value?.ToString());
                if (string.IsNullOrWhiteSpace(cn) || !seen.Add(cn))
                {
                    continue;
                }

                names.Add(cn);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }

        /// <summary>
        /// AD constructed attribute tokenGroups: security groups including nested (closest whoami equivalent for another user).
        /// </summary>
        private static string[] ReadTokenGroups(SearchResult result)
        {
            if (result == null)
            {
                return EmptyGroups;
            }

            try
            {
                using (DirectoryEntry entry = result.GetDirectoryEntry())
                {
                    entry.RefreshCache(new[] { "tokenGroups" });
                    PropertyValueCollection values = entry.Properties["tokenGroups"];
                    if (values == null || values.Count == 0)
                    {
                        return EmptyGroups;
                    }

                    var names = new List<string>(values.Count);
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (object value in values)
                    {
                        byte[] sidBytes = value as byte[];
                        if (sidBytes == null || sidBytes.Length == 0)
                        {
                            continue;
                        }

                        string name;
                        try
                        {
                            var sid = new SecurityIdentifier(sidBytes, 0);
                            name = TryTranslateSidName(sid);
                        }
                        catch
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                        {
                            continue;
                        }

                        names.Add(name);
                    }

                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    return names.ToArray();
                }
            }
            catch
            {
                return EmptyGroups;
            }
        }

        /// <summary>Extracts CN from an LDAP DN (handles simple escaped commas).</summary>
        private static string GetCnFromDn(string dn)
        {
            if (string.IsNullOrWhiteSpace(dn))
            {
                return null;
            }

            string trimmed = dn.Trim();
            if (!trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            var sb = new StringBuilder();
            for (int i = 3; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == '\\' && i + 1 < trimmed.Length)
                {
                    sb.Append(trimmed[++i]);
                    continue;
                }

                if (c == ',')
                {
                    break;
                }

                sb.Append(c);
            }

            return sb.ToString();
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

        private struct DomainUserInfo
        {
            public string FullName;
            public string[] LdapGroups;
            public string[] TokenGroups;
        }
    }
}
