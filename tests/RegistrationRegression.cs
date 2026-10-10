using System;
using System.Reflection;
using Microsoft.Win32;
using ApkShellext2;

class RegistrationRegression {
    static int checks;
    static readonly Type Helper = typeof(ApkIconHandler).Assembly.GetType("ApkShellext2.ShellRegistration", true);
    static readonly Type[] Handlers = { typeof(ApkIconHandler), typeof(ApkContextMenu), typeof(ApkInfoTipHandler), typeof(ApkThumbnailHandler) };
    static void Call(string action, Type handler, RegistryKey root) {
        try { Helper.GetMethod(action, BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(RegistryKey) }, null).Invoke(null, new object[] { handler, root }); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static void Assert(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
    static object Get(RegistryKey root, string path, string name = null) { using (var key = root.OpenSubKey(path)) return key == null ? null : key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
    static void Put(RegistryKey root, string path, object value, RegistryValueKind kind = RegistryValueKind.String) { using (var key = root.CreateSubKey(path)) key.SetValue(null, value, kind); }
    public static int Main() {
        string testPath = @"Software\ApkShellextRegistrationTests\" + Guid.NewGuid().ToString("N");
        try {
            using (var root = Registry.CurrentUser.CreateSubKey(testPath)) {
                Put(root, ".apk", "Existing.Package");
                Put(root, @"Existing.Package\DefaultIcon", @"%SystemRoot%\prior.dll,4", RegistryValueKind.ExpandString);
                Put(root, @"Existing.Package\ShellEx\IconHandler", "{11111111-1111-1111-1111-111111111111}");
                using (var key = root.CreateSubKey(@"Existing.Package\ShellEx\IconHandler")) key.SetValue("Unrelated", "keep");
                Put(root, @"CLSID\untouched\InprocServer32", "keep-native-host");
                foreach (var handler in Handlers) Call("Register", handler, root);
                foreach (var handler in Handlers) Call("Register", handler, root);
                Assert((string)Get(root, ".apk") == "Existing.Package", "Changed extension default app");
                Assert(Get(root, ".ipa") == null, "Created extension default app");
                Assert((string)Get(root, @"Existing.Package\ShellEx\IconHandler") == typeof(ApkIconHandler).GUID.ToString("B"), "Icon registration missing");
                Assert((string)Get(root, @"Existing.Package\DefaultIcon") == "%1", "DefaultIcon not enabled");
                Assert((string)Get(root, @"Existing.Package\ShellEx\ContextMenuHandlers\ApkContextMenu") == typeof(ApkContextMenu).GUID.ToString("B"), "Context menu missing");
                Assert((string)Get(root, @"Existing.Package\ShellEx\{00021500-0000-0000-C000-000000000046}") == typeof(ApkInfoTipHandler).GUID.ToString("B"), "Infotip missing");
                Assert((string)Get(root, @"Existing.Package\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}") == typeof(ApkThumbnailHandler).GUID.ToString("B"), "Thumbnail missing");
                Assert(Get(root, @".ipa\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}") == null, "IPA thumbnail unexpectedly registered");
                foreach (string ext in new[] { ".ipa", ".appx", ".appxbundle" }) Assert(Get(root, ext + @"\ShellEx\IconHandler") != null, "Missing extension icon " + ext);
                foreach (var handler in Handlers) Call("Unregister", handler, root);
                foreach (var handler in Handlers) Call("Unregister", handler, root);
                Assert((string)Get(root, @"Existing.Package\DefaultIcon") == @"%SystemRoot%\prior.dll,4", "Original default icon lost after repeated install");
                using (var key = root.OpenSubKey(@"Existing.Package\DefaultIcon")) Assert(key.GetValueKind(null) == RegistryValueKind.ExpandString, "Registry value kind lost");
                Assert((string)Get(root, @"Existing.Package\ShellEx\IconHandler") == "{11111111-1111-1111-1111-111111111111}", "Original handler lost");
                Assert((string)Get(root, @"Existing.Package\ShellEx\IconHandler", "Unrelated") == "keep", "Unrelated registry value deleted");
                Assert((string)Get(root, @"CLSID\untouched\InprocServer32") == "keep-native-host", "Changed native host registration");
                Assert(Get(root, @".ipa\ShellEx\IconHandler") == null, "Absent handler value not restored");
                Call("Register", typeof(ApkIconHandler), root);
                Put(root, @"Existing.Package\ShellEx\IconHandler", "third-party");
                Call("Unregister", typeof(ApkIconHandler), root);
                Assert((string)Get(root, @"Existing.Package\ShellEx\IconHandler") == "third-party", "Removed third-party takeover");
                Assert((string)Get(root, @"Existing.Package\DefaultIcon") == "%1", "Overwrote takeover default icon");
                Call("Register", typeof(ApkIconHandler), root);
                Put(root, @"Existing.Package\ShellEx\IconHandler", "newer-third-party");
                Call("Register", typeof(ApkIconHandler), root);
                Call("Unregister", typeof(ApkIconHandler), root);
                Assert((string)Get(root, @"Existing.Package\ShellEx\IconHandler") == "newer-third-party", "Reacquired registration lost current predecessor");
                Call("Register", typeof(ApkContextMenu), root);
                Put(root, ".apk", "Changed.Package");
                Call("Unregister", typeof(ApkContextMenu), root);
                Assert(Get(root, @"Existing.Package\ShellEx\ContextMenuHandlers\ApkContextMenu") == null, "Original registered target leaked after ProgID change");
                Assert((string)Get(root, ".apk") == "Changed.Package", "Unregister changed default app");
                bool rejected = false;
                try { Call("Register", typeof(string), root); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "Unrelated type accepted");
                using (var journal = root.OpenSubKey("ApkShellext2.Registration")) Assert(journal == null, "Registration journal leaked");
            }
            Console.WriteLine("PASS bounded registration checks=" + checks); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Registry.CurrentUser.DeleteSubKeyTree(testPath, false); }
    }
}
