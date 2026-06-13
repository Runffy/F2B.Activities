using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace F2B.Basic
{
    internal static class WindowsCredentialManager
    {
        internal static WindowsCredential ReadGenericCredential(string credentialName)
        {
            return ReadWithCandidates(
                credentialName,
                NativeCredentialType.Generic,
                BuildGenericTargetCandidates(credentialName));
        }

        private static WindowsCredential ReadWithCandidates(
            string credentialName,
            NativeCredentialType credentialType,
            IReadOnlyList<string> targetCandidates)
        {
            int lastError = 0;
            foreach (string target in targetCandidates)
            {
                if (CredRead(target, (uint)credentialType, 0, out IntPtr credentialPtr))
                {
                    using (var handle = new CriticalCredentialHandle(credentialPtr))
                    {
                        return ReadCredential(handle.GetCredential());
                    }
                }

                lastError = Marshal.GetLastWin32Error();
            }

            throw new Win32Exception(
                lastError,
                string.Format(
                    "Generic credential '{0}' was not found or could not be read. Win32 error: {1}.",
                    credentialName,
                    lastError));
        }

        private static IReadOnlyList<string> BuildGenericTargetCandidates(string credentialName)
        {
            var candidates = new List<string>();
            AddUnique(candidates, credentialName);

            if (!credentialName.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
            {
                AddUnique(candidates, "target:" + credentialName);
            }

            if (!credentialName.StartsWith("LegacyGeneric:target=", StringComparison.OrdinalIgnoreCase))
            {
                AddUnique(candidates, "LegacyGeneric:target=" + credentialName);
            }

            return candidates;
        }

        private static void AddUnique(ICollection<string> candidates, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (string existing in candidates)
            {
                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(value);
        }

        private static WindowsCredential ReadCredential(CREDENTIAL credential)
        {
            string targetName = ReadUnicodeString(credential.TargetName);
            string userName = ReadUnicodeString(credential.UserName);
            string password = ReadPasswordBlob(credential.CredentialBlob, credential.CredentialBlobSize);

            return new WindowsCredential(targetName, userName, password);
        }

        private static string ReadUnicodeString(IntPtr pointer)
        {
            return pointer == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUni(pointer) ?? string.Empty);
        }

        private static string ReadPasswordBlob(IntPtr blob, uint blobSize)
        {
            if (blob == IntPtr.Zero || blobSize == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[blobSize];
            Marshal.Copy(blob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
        private static extern bool CredFree([In] IntPtr cred);

        private enum NativeCredentialType
        {
            Generic = 1,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        private sealed class CriticalCredentialHandle : CriticalHandleZeroOrMinusOneIsInvalid
        {
            public CriticalCredentialHandle(IntPtr preexistingHandle)
            {
                SetHandle(preexistingHandle);
            }

            public CREDENTIAL GetCredential()
            {
                if (IsInvalid)
                {
                    throw new InvalidOperationException("Invalid credential handle.");
                }

                return (CREDENTIAL)Marshal.PtrToStructure(handle, typeof(CREDENTIAL));
            }

            protected override bool ReleaseHandle()
            {
                if (!IsInvalid)
                {
                    CredFree(handle);
                    SetHandleAsInvalid();
                    return true;
                }

                return false;
            }
        }
    }

    internal sealed class WindowsCredential
    {
        public WindowsCredential(string targetName, string userName, string password)
        {
            TargetName = targetName;
            UserName = userName;
            Password = password;
        }

        public string TargetName { get; }

        public string UserName { get; }

        public string Password { get; }
    }
}
