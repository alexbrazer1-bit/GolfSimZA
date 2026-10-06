// SimLink.exe - starts the SimLink Control Panel (SimLink-Panel.ps1 in the same folder) without a console window.
// Build on Windows: %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32icon:simlink.ico /out:SimLink.exe SimLinkLauncher.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("SimLink Control Panel")]
[assembly: AssemblyProduct("SimLink")]
[assembly: AssemblyDescription("Control the GolfSimZA simulator PC from the main PC")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

internal static class SimLinkLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string script = Path.Combine(dir, "SimLink-Panel.ps1");
            if (!File.Exists(script))
            {
                MessageBox.Show("SimLink-Panel.ps1 was not found next to SimLink.exe:\r\n" + dir +
                    "\r\n\r\nReinstall with SimLink-MainPC-Setup.exe.", "SimLink", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            string windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
            // Use the 64-bit PowerShell even if this launcher were started as a 32-bit process.
            string ps = Path.Combine(windir, Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                ? @"Sysnative\WindowsPowerShell\v1.0\powershell.exe"
                : @"System32\WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(ps)) ps = "powershell.exe";
            string extra = args.Length > 0 ? " " + string.Join(" ", args) : "";
            var psi = new ProcessStartInfo(ps,
                "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + script + "\"" + extra);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = dir;
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("SimLink could not start:\r\n" + ex.Message, "SimLink", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
    }
}
