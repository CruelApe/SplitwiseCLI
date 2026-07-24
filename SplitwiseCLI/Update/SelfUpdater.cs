using System.Diagnostics;
using System.IO.Compression;

namespace SplitwiseCLI.Update;

// Applies a downloaded release zip in place. A running exe can't overwrite its
// own files, so this extracts the new build to a staging directory, then hands
// off to a small detached PowerShell script that waits for *this* process to
// exit before copying the staged files over the install directory and cleaning
// up after itself. Nothing here relaunches the app - the user runs 'splitwise'
// again whenever they next need it, picking up the new build automatically.
public static class SelfUpdater
{
    public static void ScheduleApply(string zipPath, string installDirectory)
    {
        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI-update-{Guid.NewGuid():N}");
        ZipFile.ExtractToDirectory(zipPath, stagingDirectory);

        var scriptPath = Path.Combine(Path.GetTempPath(), $"SplitwiseCLI-apply-{Guid.NewGuid():N}.ps1");
        var script = BuildApplyScript(Environment.ProcessId, stagingDirectory, installDirectory, zipPath);
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static string BuildApplyScript(int processId, string stagingDirectory, string installDirectory, string zipPath) => """
        $ErrorActionPreference = 'Stop'
        try { Wait-Process -Id __PID__ -ErrorAction SilentlyContinue } catch {}
        Start-Sleep -Milliseconds 500
        Copy-Item -Path (Join-Path "__STAGING__" '*') -Destination "__INSTALLDIR__" -Recurse -Force
        Remove-Item -Path "__STAGING__" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "__ZIP__" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
        """
        .Replace("__PID__", processId.ToString())
        .Replace("__STAGING__", stagingDirectory)
        .Replace("__INSTALLDIR__", installDirectory)
        .Replace("__ZIP__", zipPath);
}
