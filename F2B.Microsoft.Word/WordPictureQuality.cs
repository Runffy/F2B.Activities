using System;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace F2B.Microsoft.Word
{
    /// <summary>
    /// Word auto-compresses embedded pictures on Save (default ~220 ppi at the
    /// picture's display size). That is why AutoFit/Custom large images look
    /// much softer than a manual paste when "Do not compress images" is on.
    /// Interop has no property for this; we set the Word default via registry
    /// for the session and stamp w:doNotAutoCompressPictures into docx packages.
    /// </summary>
    internal static class WordPictureQuality
    {
        private const string WordMlNamespace =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        private const string DoNotCompressLocalName = "doNotAutoCompressPictures";
        private const string RegistryValueName = "AutomaticPictureCompressionDefault";

        private static readonly string[] OfficeVersionFolders =
        {
            "16.0", "15.0", "14.0", "12.0"
        };

        internal static IDisposable PreferFullFidelity()
        {
            // Registry drives Word's Advanced option "Do not compress images in file"
            // (AutomaticPictureCompressionDefault). Must stay set through Document.Save.
            return TrySetAutomaticPictureCompressionDefault(0) ?? NoOpDisposable.Instance;
        }

        internal static void TryEnsureDocxOnDisk(string wordFilePath)
        {
            if (string.IsNullOrWhiteSpace(wordFilePath))
            {
                return;
            }

            string path;
            try
            {
                path = Path.GetFullPath(wordFilePath);
            }
            catch
            {
                return;
            }

            if (!File.Exists(path))
            {
                return;
            }

            if (!string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                EnsureDoNotAutoCompressPicturesInPackage(path);
            }
            catch
            {
                // File may be locked by Word or not a valid OPC package; ignore.
            }
        }

        private static IDisposable TrySetAutomaticPictureCompressionDefault(int value)
        {
            var restorers = new System.Collections.Generic.List<RegistryValueRestorer>();
            try
            {
                foreach (string version in OfficeVersionFolders)
                {
                    string keyPath = @"Software\Microsoft\Office\" + version + @"\Word\Options";
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        if (key == null)
                        {
                            continue;
                        }

                        object previous = key.GetValue(RegistryValueName);
                        RegistryValueKind previousKind = RegistryValueKind.DWord;
                        bool hadValue = previous != null;
                        if (hadValue)
                        {
                            try
                            {
                                previousKind = key.GetValueKind(RegistryValueName);
                            }
                            catch
                            {
                                previousKind = RegistryValueKind.DWord;
                            }
                        }

                        key.SetValue(RegistryValueName, value, RegistryValueKind.DWord);
                        restorers.Add(new RegistryValueRestorer(keyPath, RegistryValueName, hadValue, previous, previousKind));
                    }
                }
            }
            catch
            {
                for (int i = restorers.Count - 1; i >= 0; i--)
                {
                    restorers[i].Dispose();
                }

                return null;
            }

            if (restorers.Count == 0)
            {
                return null;
            }

            return new CompositeDisposable(restorers.ToArray());
        }

        private static void EnsureDoNotAutoCompressPicturesInPackage(string docxPath)
        {
            using (Package package = Package.Open(docxPath, FileMode.Open, FileAccess.ReadWrite))
            {
                Uri settingsUri = new Uri("/word/settings.xml", UriKind.Relative);
                if (!package.PartExists(settingsUri))
                {
                    return;
                }

                PackagePart settingsPart = package.GetPart(settingsUri);
                var document = new XmlDocument { PreserveWhitespace = true };
                using (Stream stream = settingsPart.GetStream(FileMode.Open, FileAccess.Read))
                {
                    document.Load(stream);
                }

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
                nsmgr.AddNamespace("w", WordMlNamespace);

                XmlNode settingsNode = document.SelectSingleNode("/w:settings", nsmgr);
                if (settingsNode == null)
                {
                    return;
                }

                XmlNode existing = settingsNode.SelectSingleNode("w:" + DoNotCompressLocalName, nsmgr);
                if (existing != null)
                {
                    // Present without w:val, or w:val=true/1/on → already enabled.
                    XmlAttribute val = existing.Attributes?["val", WordMlNamespace]
                        ?? existing.Attributes?["w:val"];
                    if (val == null || IsOnOffTrue(val.Value))
                    {
                        return;
                    }

                    val.Value = "1";
                }
                else
                {
                    XmlElement element = document.CreateElement("w", DoNotCompressLocalName, WordMlNamespace);
                    settingsNode.AppendChild(element);
                }

                using (Stream stream = settingsPart.GetStream(FileMode.Create, FileAccess.Write))
                using (var writer = new XmlTextWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Formatting = Formatting.None;
                    document.Save(writer);
                }
            }
        }

        private static bool IsOnOffTrue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class NoOpDisposable : IDisposable
        {
            internal static readonly NoOpDisposable Instance = new NoOpDisposable();

            public void Dispose()
            {
            }
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly IDisposable[] _items;
            private bool _disposed;

            internal CompositeDisposable(IDisposable[] items)
            {
                _items = items;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                for (int i = _items.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        _items[i].Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private sealed class RegistryValueRestorer : IDisposable
        {
            private readonly string _keyPath;
            private readonly string _valueName;
            private readonly bool _hadValue;
            private readonly object _previous;
            private readonly RegistryValueKind _previousKind;
            private bool _disposed;

            internal RegistryValueRestorer(
                string keyPath,
                string valueName,
                bool hadValue,
                object previous,
                RegistryValueKind previousKind)
            {
                _keyPath = keyPath;
                _valueName = valueName;
                _hadValue = hadValue;
                _previous = previous;
                _previousKind = previousKind;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
                    {
                        if (key == null)
                        {
                            return;
                        }

                        if (_hadValue)
                        {
                            key.SetValue(_valueName, _previous ?? 0, _previousKind);
                        }
                        else
                        {
                            key.DeleteValue(_valueName, throwOnMissingValue: false);
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
