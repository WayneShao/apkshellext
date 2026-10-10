using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using ApkShellext2;

class ReliabilityTests {
    static string root;
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static byte[] Text(string value) { return Encoding.UTF8.GetBytes(value); }
    static byte[] Zip(string name, byte[] data) {
        using (var m = new MemoryStream()) {
            using (var z = new ZipArchive(m, ZipArchiveMode.Create, true))
                if (name != null) using (var s = z.CreateEntry(name, CompressionLevel.Optimal).Open()) s.Write(data, 0, data.Length);
            return m.ToArray();
        }
    }
    static string Save(string name, byte[] data) { var p = Path.Combine(root, name); File.WriteAllBytes(p, data); return p; }
    static byte[] Appx(bool phone = false) {
        return Zip("AppxManifest.xml", Text("<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='Sample' Version='1.2.3.4' Publisher='CN=Example'/><Properties><DisplayName>Sample</DisplayName><Logo>logo.png</Logo></Properties>" + (phone ? "<mp:PhoneIdentity xmlns:mp='urn:phone' PhoneProductId='sample'/>" : "") + "</Package>"));
    }
    static byte[] Bundle(bool application) {
        using (var m = new MemoryStream()) {
            using (var z = new ZipArchive(m, ZipArchiveMode.Create, true)) {
                using (var s = z.CreateEntry("AppxMetadata/AppxBundleManifest.xml").Open()) {
                    var b = Text("<Bundle><Identity Name='Sample'/><Packages><Package Type='" + (application ? "application" : "resource") + "' FileName='inside.appx'/></Packages></Bundle>"); s.Write(b, 0, b.Length);
                }
                using (var s = z.CreateEntry("inside.appx", CompressionLevel.Optimal).Open()) { var b = Appx(); s.Write(b, 0, b.Length); }
            }
            return m.ToArray();
        }
    }
    static void FailureUnlock(string ext, byte[] bytes) {
        var p = Save("bad." + ext, bytes); bool threw = false;
        try { using (var r = AppPackageReader.Read(p)) { } } catch { threw = true; }
        Assert(threw, "expected invalid package failure");
        using (var f = new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        File.Delete(p);
    }
    public static int Main(string[] args) {
        root = Path.Combine(Path.GetTempPath(), "ApkShellTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            var scenario = args[0];
            if (scenario.StartsWith("unlock-")) FailureUnlock(scenario.Substring(7), Zip(null, null));
            else if (scenario == "upper") Assert(AppPackageReader.getAppType("test.APK") == AppPackageReader.AppType.AndroidApp, "uppercase APK");
            else if (scenario == "appx-desktop") { using (var r = new AppxReader(Save("valid.appx", Appx()))) Assert(r.AppName == "Sample" && r.AppID == "", "desktop APPX metadata"); }
            else if (scenario == "bundle-eof") FailureUnlock("appxbundle", Bundle(false));
            else if (scenario == "bundle-deflated") { using (var r = new AppxBundleReader(Save("valid.appxbundle", Bundle(true)))) Assert(r.AppName == "Sample", "nested APPX metadata"); }
            else if (scenario == "writer-sharing") {
                var p = Save("download.appx", Appx(true));
                using (var r = new AppxReader(p)) {
                    using (var writer = new FileStream(p, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { }
                    File.Move(p, p + ".renamed");
                }
            }
            else if (scenario == "update-first-run") {
                var m = typeof(Utility).GetMethod("IsUpdateDue", BindingFlags.Static | BindingFlags.NonPublic);
                Assert(m != null, "missing update due helper");
                Assert((bool)m.Invoke(null, new object[] { "0", DateTime.Today }), "invalid/missing timestamp must allow update");
                Assert(!(bool)m.Invoke(null, new object[] { DateTime.Today.ToString("yyyy-MM-dd"), DateTime.Today }), "same-day update must not repeat");
            }
            else if (scenario == "version-invalid") {
                var m = typeof(Utility).GetMethod("IsNewerVersion", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                Assert(m != null, "missing pure version comparator");
                Assert(!(bool)m.Invoke(null, new object[] { "a.b.c.d", new Version(1, 0, 0, 0) }), "invalid version must not throw");
                Assert((bool)m.Invoke(null, new object[] { "1.0.0.1", new Version(1, 0, 0, 0) }), "newer version");
                Assert(!(bool)m.Invoke(null, new object[] { "0.9.9.9", new Version(1, 0, 0, 0) }), "older version");
            }
            else throw new Exception("unknown scenario");
            Console.WriteLine("PASS " + scenario); return 0;
        } catch (Exception e) { Console.Error.WriteLine("FAIL " + args[0] + ": " + e); return 1; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
