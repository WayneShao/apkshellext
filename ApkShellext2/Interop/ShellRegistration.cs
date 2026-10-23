using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ApkShellext2 {
    // Native comhost owns CLSID/InprocServer32. This helper owns only association values.
    internal static class ShellRegistration {
        private const string Journal = "ApkShellext2.Registration";
        private static readonly string[] Extensions = { ".apk", ".ipa", ".appx", ".appxbundle" };
        private sealed class Handler {
            internal string Id, Slot;
            internal bool Icon, Thumbnail;
        }
        private static Handler Describe(Type type) {
            if (type == typeof(ApkIconHandler)) return new Handler { Id = type.GUID.ToString("B"), Slot = @"ShellEx\IconHandler", Icon = true };
            if (type == typeof(ApkContextMenu)) return new Handler { Id = type.GUID.ToString("B"), Slot = @"ShellEx\ContextMenuHandlers\ApkContextMenu" };
            if (type == typeof(ApkInfoTipHandler)) return new Handler { Id = type.GUID.ToString("B"), Slot = @"ShellEx\{00021500-0000-0000-C000-000000000046}" };
            if (type == typeof(ApkThumbnailHandler)) return new Handler { Id = type.GUID.ToString("B"), Slot = @"ShellEx\{e357fccd-a995-4576-b01f-234630154e96}", Thumbnail = true };
            throw new ArgumentException("Only the four package shell handlers can be registered.", "type");
        }
        internal static void Register(Type type) {
            Describe(type);
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32))
            using (var classes = machine.CreateSubKey(@"Software\Classes")) Register(type, classes);
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32))
            using (var classes = user.CreateSubKey(@"Software\Classes"))
            using (var lookup = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32))
                RegisterCore(type, classes, lookup);
            Notify();
        }
        internal static void Unregister(Type type) {
            Describe(type);
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32))
            using (var classes = machine.OpenSubKey(@"Software\Classes", true)) {
                if (classes != null) Unregister(type, classes);
            }
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32))
            using (var classes = user.OpenSubKey(@"Software\Classes", true)) {
                if (classes != null) Unregister(type, classes);
            }
            Notify();
        }
        // Injected root keeps regression tests outside production association keys.
        internal static void Register(Type type, RegistryKey classes) {
            RegisterCore(type, classes, classes);
        }
        // The per-user destination is distinct from the effective ProgID lookup.
        // Each hive keeps its own predecessor journal; default applications are untouched.
        internal static void RegisterCore(Type type, RegistryKey classes, RegistryKey lookupClasses) {
            Handler handler = Describe(type);
            if (classes == null) throw new ArgumentNullException("classes");
            if (lookupClasses == null) throw new ArgumentNullException("lookupClasses");
            using (var journal = classes.CreateSubKey(Journal + "\\" + handler.Id)) {
                foreach (string extension in handler.Thumbnail ? new[] { ".apk" } : Extensions) {
                    string association = extension;
                    using (var key = lookupClasses.OpenSubKey(extension)) {
                        string progId = key == null ? null : key.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                        // Never follow a registry path supplied as a purported ProgID.
                        if (!string.IsNullOrWhiteSpace(progId) && progId.IndexOfAny(new[] { '\\', '/', '\0' }) < 0 && !progId.Equals("CLSID", StringComparison.OrdinalIgnoreCase)) association = progId;
                    }
                    string slot = association + "\\" + handler.Slot;
                    SetOwnedValue(classes, journal, slot, handler.Id, null);
                    if (handler.Icon) SetOwnedValue(classes, journal, association + @"\DefaultIcon", "%1", slot);
                }
            }
        }
        private static string KeyFor(string target) {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(target.ToUpperInvariant()))).Replace("-", "");
        }
        private static bool Matches(RegistryKey classes, string target, string expected) {
            using (var key = classes.OpenSubKey(target)) return key != null && string.Equals(key.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string, expected, StringComparison.OrdinalIgnoreCase);
        }
        private static void SetOwnedValue(RegistryKey classes, RegistryKey journal, string target, string value, string requiredSlot) {
            string record = KeyFor(target);
            using (var key = classes.CreateSubKey(target)) {
                using (var existing = journal.OpenSubKey(record)) {
                    if (existing == null || !string.Equals(key.GetValue(null) as string, value, StringComparison.OrdinalIgnoreCase)) {
                        using (var backup = journal.CreateSubKey(record)) {
                            backup.SetValue("Target", target);
                            backup.SetValue("Owned", value);
                            if (requiredSlot != null) backup.SetValue("RequiredSlot", requiredSlot);
                            bool existed = key.GetValueNames().Contains("");
                            backup.SetValue("HadValue", existed ? 1 : 0, RegistryValueKind.DWord);
                            if (existed) {
                                RegistryValueKind kind = key.GetValueKind(null);
                                backup.SetValue("Kind", (int)kind, RegistryValueKind.DWord);
                                backup.SetValue("Previous", key.GetValue(null, null, RegistryValueOptions.DoNotExpandEnvironmentNames), kind);
                            }
                        }
                    }
                }
                key.SetValue(null, value, RegistryValueKind.String);
            }
        }
        internal static void Unregister(Type type, RegistryKey classes) {
            Handler handler = Describe(type);
            if (classes == null) throw new ArgumentNullException("classes");
            string path = Journal + "\\" + handler.Id;
            using (var journal = classes.OpenSubKey(path, true)) {
                if (journal == null) return;
                string[] records = journal.GetSubKeyNames();
                var permitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                // Evaluate icon ownership before restoring the icon-handler slot.
                foreach (string name in records) using (var record = journal.OpenSubKey(name)) {
                    string target = record.GetValue("Target") as string;
                    string owned = record.GetValue("Owned") as string;
                    string dependency = record.GetValue("RequiredSlot") as string;
                    if (target != null && owned != null && Matches(classes, target, owned) && (dependency == null || Matches(classes, dependency, handler.Id))) permitted.Add(name);
                }
                foreach (string name in records) {
                    using (var record = journal.OpenSubKey(name)) {
                        string target = record.GetValue("Target") as string;
                        if (permitted.Contains(name)) using (var key = classes.OpenSubKey(target, true)) {
                            // Recheck immediately before writing; a takeover is not ours to undo.
                            if (key != null && string.Equals(key.GetValue(null) as string, record.GetValue("Owned") as string, StringComparison.OrdinalIgnoreCase)) {
                                if ((int)record.GetValue("HadValue", 0) != 0) key.SetValue(null, record.GetValue("Previous", null, RegistryValueOptions.DoNotExpandEnvironmentNames), (RegistryValueKind)(int)record.GetValue("Kind"));
                                else key.DeleteValue(null, false);
                            }
                        }
                    }
                    journal.DeleteSubKeyTree(name, false);
                }
            }
            // Only our private journal tree is removed. Association containers and
            // unrelated values/subkeys remain intact, including third-party takeovers.
            classes.DeleteSubKey(path, false);
            using (var root = classes.OpenSubKey(Journal)) {
                if (root == null || root.SubKeyCount != 0 || root.ValueCount != 0) return;
            }
            classes.DeleteSubKey(Journal, false);
        }
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
        private static void Notify() { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); }
    }
}
