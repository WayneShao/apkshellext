using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using ApkShellext2;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string fixture = Path.Combine(Path.GetTempPath(), "Worker-menu-" + Guid.NewGuid().ToString("N") + ".apk");
        // Prevent menu construction's existing background update from contacting the network.
        using var settings = Registry.CurrentUser.CreateSubKey(@"Software\ApkShellext2");
        object previous = settings.GetValue("LastCheckUpdateTime", null);
        RegistryValueKind kind = previous == null ? RegistryValueKind.String : settings.GetValueKind("LastCheckUpdateTime");
        settings.SetValue("LastCheckUpdateTime", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        try
        {
            File.WriteAllText(fixture, "Malformed package fixture");
            Application.EnableVisualStyles();
            Type window = Assembly.Load("ApkShellext2.Worker").GetType("ApkShellext2.Worker.Program+MenuWindow", true);
            foreach (string[] selection in new[] { new[] { fixture }, new[] { fixture, fixture } })
            {
                using var form = (Form)Activator.CreateInstance(window, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { selection }, null);
                if (form.MainMenuStrip.Items.Count != 1) throw new Exception("Missing package menu");
                var main = (ToolStripMenuItem)form.MainMenuStrip.Items[0];
                if (main.DropDownItems.Count < 4) throw new Exception("Legacy actions lost");
                var preferences = main.DropDownItems[main.DropDownItems.Count - 1] as ToolStripMenuItem;
                if (preferences == null || !preferences.Enabled || string.IsNullOrWhiteSpace(preferences.Text))
                    throw new Exception("Preferences unavailable");
                Console.WriteLine("PASS menu construction, selection count " + selection.Length + ": " + main.DropDownItems.Count + " top-level actions; preferences " + preferences.Text);
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            File.Delete(fixture);
            if (previous == null) settings.DeleteValue("LastCheckUpdateTime", false);
            else settings.SetValue("LastCheckUpdateTime", previous, kind);
        }
    }
}
