using System.Diagnostics;

namespace SplitwiseCLI.Update;

public sealed record ProcessResult(bool Success, string Output);

// Drives the actual "update won't touch any files" workaround for a
// GlobalTool install: git pull, dotnet pack, dotnet tool update, all run
// inside the local checkout that LocalCheckoutLocator found. Split into
// discrete steps (rather than one shelled-out script) so UpdateCommand can
// report progress and, on failure, say exactly which step failed.
public static class GlobalToolUpdater
{
    public static Task<ProcessResult> RunGitPullAsync(string repoRoot, CancellationToken cancellationToken) =>
        // --ff-only: this drives an unattended reinstall, so it should never
        // invent a merge commit or silently take a side of a conflict - a
        // repo that's diverged from origin should stop here and let the
        // (human) maintainer sort it out.
        RunAsync("git", "pull --ff-only", repoRoot, cancellationToken);

    public static Task<ProcessResult> RunDotnetPackAsync(string repoRoot, CancellationToken cancellationToken) =>
        RunAsync("dotnet", "pack SplitwiseCLI -o nupkg", repoRoot, cancellationToken);

    public static Task<ProcessResult> RunToolUpdateAsync(string repoRoot, CancellationToken cancellationToken) =>
        RunAsync("dotnet", "tool update --global --add-source nupkg SplitwiseCLI", repoRoot, cancellationToken);

    // Resolves the 'origin' remote of the git repo at `repoRoot`, or null if
    // there isn't one / git isn't on PATH / the directory isn't a repo.
    // Synchronous and best-effort: it's a quick local git call used only to
    // confirm a candidate directory is really this project's clone.
    public static string? GetRemoteOriginUrl(string repoRoot)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git", "remote get-url origin")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{fileName} {arguments}'.");

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = string.Join(Environment.NewLine, await stdOutTask, await stdErrTask).Trim();
            return new ProcessResult(process.ExitCode == 0, output);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessResult(false, $"Failed to run '{fileName} {arguments}': {ex.Message}");
        }
    }
}
