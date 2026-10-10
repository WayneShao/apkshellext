using System;
using System.Collections;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using ApkShellext2;

class MigrationUiRegression
{
    static int checks;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Handles(Control root)
    {
        Require(root.Handle != IntPtr.Zero, "Control handle failed: " + root.Name);
        foreach (Control child in root.Controls) Handles(child);
    }
    [STAThread]
    static int Main()
    {
        try {
            Require(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA, "UI test requires STA");
            Require(Path.GetFullPath(Utility.getInstallPath()) == Path.GetDirectoryName(typeof(Utility).Assembly.Location), "Install path differs from loaded DLL location");
            var strings = new ResourceManager("ApkShellext2.Properties.Resources", typeof(Utility).Assembly);
            Require(strings.GetString("btnOK", CultureInfo.GetCultureInfo("zh-CN")) == "\u786e\u5b9a", "Chinese satellite resource failed");
            Require(strings.GetString("strPreferencesCaption", CultureInfo.InvariantCulture)?.Length > 0, "Default preferences string missing");
            int cultures = 0;
            foreach (var culture in Utility.getSupportedLanguages()) {
                Require(strings.GetString("strPreferencesCaption", culture)?.Length > 0, "Missing preferences caption: " + culture.Name);
                cultures++;
            }
            Require(cultures >= 16, "Satellite resource directories missing: " + cultures);
            var images = new ResourceManager("ApkShellext2.Properties.NonLocalizeResources", typeof(Utility).Assembly);
            int bitmaps = 0;
            foreach (DictionaryEntry entry in images.GetResourceSet(CultureInfo.InvariantCulture, true, false)) {
                if (entry.Value is Bitmap bitmap) {
                    Require(bitmap.Width > 0 && bitmap.Height > 0, "Invalid Bitmap resource: " + entry.Key);
                    using (var resized = Utility.ResizeBitmap(bitmap, 16)) Require(resized.Width == 16 && resized.Height == 16, "Bitmap resize failed: " + entry.Key);
                    bitmaps++;
                }
            }
            Require(bitmaps > 0, "No Bitmap resources decoded");
            using (var form = new Preferences()) {
                Require(!form.Visible, "Preferences unexpectedly shown");
                Handles(form);
                Require(!form.Visible && form.Controls.Count > 0, "Preferences hidden construction failed");
            }
            Console.WriteLine("PASS migration UI checks=" + checks + " cultures=" + cultures + " bitmaps=" + bitmaps + " hidden Preferences/control handles STA");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
