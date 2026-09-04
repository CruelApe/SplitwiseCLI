using System.Security.Cryptography;
using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Update;

namespace SplitwiseCLI.Cli;

public sealed class UpdateCommand : AsyncCommand<UpdateCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, UpdateCommandSettings settings, CancellationToken cancellationToken)
    {
        var (owner, repo) = ParseRepository(AppInfo.RepositoryUrl);
        using var httpClient = new HttpClient();
        var releaseClient = new GitHubReleaseClient(httpClient);

        var release = await AnsiConsole.Status()
            .StartAsync("Checking for updates...", _ => releaseClient.GetLatestReleaseAsync(owner, repo, cancellationToken));

        var currentVersion = AppInfo.Version;
        if (!VersionComparer.IsNewer(release.TagName, currentVersion))
        {
            AnsiConsole.MarkupLine($"You're running the latest version ([green]{currentVersion.EscapeMarkup()}[/]).");
            return 0;
        }

        AnsiConsole.MarkupLine(
            $"A new version is available: [green]{release.TagName.EscapeMarkup()}[/] (you have [grey]{currentVersion.EscapeMarkup()}[/]).");
        if (!string.IsNullOrWhiteSpace(release.Body))
        {
            AnsiConsole.MarkupLine("[bold]Release notes:[/]");
            AnsiConsole.WriteLine(release.Body.Trim());
        }

        var installMethod = InstallMethodDetector.Detect();
        if (installMethod != InstallMethod.ReleaseZip)
        {
            if (installMethod == InstallMethod.GlobalTool)
            {
                var repoRoot = LocalCheckoutLocator.Find(
                    Directory.GetCurrentDirectory(), AppInfo.RepositoryUrl, GlobalToolUpdater.GetRemoteOriginUrl);
                if (repoRoot is not null)
                {
                    return await UpdateGlobalToolFromSourceAsync(repoRoot, release.TagName, settings, cancellationToken);
                }
            }

            PrintManualUpdateInstructions(installMethod);
            return 0;
        }

        if (settings.CheckOnly)
        {
            AnsiConsole.MarkupLine("Run [bold]splitwise update[/] to install it.");
            return 0;
        }

        var assetName = $"SplitwiseCLI-{release.TagName}-win-x64.zip";
        var asset = release.Assets.FirstOrDefault(a => string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            AnsiConsole.MarkupLine($"[red]No '{assetName.EscapeMarkup()}' asset found on the {release.TagName.EscapeMarkup()} release - can't self-update.[/]");
            return 1;
        }

        if (!settings.Yes && !AnsiConsole.Confirm($"Download and install {release.TagName}?"))
        {
            AnsiConsole.MarkupLine("[yellow]Aborted - no changes made.[/]");
            return 1;
        }

        var zipPath = Path.Combine(Path.GetTempPath(), asset.Name);
        await AnsiConsole.Progress().StartAsync(async ctx =>
        {
            var task = ctx.AddTask($"Downloading {asset.Name}");
            await DownloadFileAsync(httpClient, asset.DownloadUrl, zipPath, percent => task.Value = percent, cancellationToken);
        });

        if (!await VerifyChecksumAsync(httpClient, release, asset.Name, zipPath, cancellationToken))
        {
            return 1;
        }

        var installDirectory = AppContext.BaseDirectory;
        SelfUpdater.ScheduleApply(zipPath, installDirectory);

        AnsiConsole.MarkupLine(
            $"[green]Update staged.[/] SplitwiseCLI will finish updating to {release.TagName.EscapeMarkup()} in the background " +
            "as soon as this process exits. Run 'splitwise version' in a few seconds to confirm.");
        return 0;
    }

    private static (string Owner, string Repo) ParseRepository(string repositoryUrl)
    {
        var segments = new Uri(repositoryUrl).AbsolutePath.Trim('/').Split('/');
        return (segments[0], segments[1]);
    }

    // GlobalTool installs can't be file-replaced (their real files live in the
    // NuGet tool store), but when 'update' is run from inside the maintainer's
    // own local clone, the same git-pull + dotnet-pack + dotnet-tool-update
    // sequence PrintManualUpdateInstructions tells everyone else to run by hand
    // can just be run automatically here.
    private static async Task<int> UpdateGlobalToolFromSourceAsync(
        string repoRoot, string targetTag, UpdateCommandSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine(
            $"You installed SplitwiseCLI as a .NET global tool, but this looks like your local checkout at [grey]{repoRoot.EscapeMarkup()}[/] - " +
            "it can be pulled, repacked, and reinstalled automatically.");

        if (settings.CheckOnly)
        {
            AnsiConsole.MarkupLine("Run [bold]splitwise update[/] to pull, repack, and reinstall it.");
            return 0;
        }

        if (!settings.Yes && !AnsiConsole.Confirm($"Pull the latest changes, repack, and reinstall the global tool ({targetTag})?"))
        {
            AnsiConsole.MarkupLine("[yellow]Aborted - no changes made.[/]");
            return 1;
        }

        var steps = new (string Label, Func<Task<ProcessResult>> Run)[]
        {
            ("Pulling latest changes (git pull --ff-only)...", () => GlobalToolUpdater.RunGitPullAsync(repoRoot, cancellationToken)),
            ("Packing the tool (dotnet pack)...", () => GlobalToolUpdater.RunDotnetPackAsync(repoRoot, cancellationToken)),
            ("Reinstalling the global tool (dotnet tool update)...", () => GlobalToolUpdater.RunToolUpdateAsync(repoRoot, cancellationToken)),
        };

        foreach (var (label, run) in steps)
        {
            var result = await AnsiConsole.Status().StartAsync(label, _ => run());
            if (!result.Success)
            {
                AnsiConsole.MarkupLine($"[red]'{label}' failed - nothing further was changed.[/]");
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    AnsiConsole.WriteLine(result.Output);
                }

                AnsiConsole.MarkupLine("Resolve the issue above, or fall back to running these manually:");
                PrintManualUpdateInstructions(InstallMethod.GlobalTool);
                return 1;
            }
        }

        AnsiConsole.MarkupLine(
            $"[green]Updated.[/] Run [bold]splitwise about[/] to confirm you're now on {targetTag.EscapeMarkup()}.");
        return 0;
    }

    private static void PrintManualUpdateInstructions(InstallMethod method)
    {
        switch (method)
        {
            case InstallMethod.GlobalTool:
                AnsiConsole.MarkupLine(
                    "You installed SplitwiseCLI as a .NET global tool, so this command can't replace its own files. Update it with:");
                AnsiConsole.MarkupLine("  [grey]git pull[/]");
                AnsiConsole.MarkupLine("  [grey]dotnet pack SplitwiseCLI -o nupkg[/]");
                AnsiConsole.MarkupLine("  [grey]dotnet tool update --global --add-source nupkg SplitwiseCLI[/]");
                break;
            case InstallMethod.RunFromSource:
                AnsiConsole.MarkupLine("You're running from source, so there's no standalone binary for this command to replace. Update it with:");
                AnsiConsole.MarkupLine("  [grey]git pull[/]");
                break;
        }
    }

    private static async Task DownloadFileAsync(
        HttpClient client, string url, string destinationPath, Action<double> onProgress, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;
            if (totalBytes > 0)
            {
                onProgress(totalRead * 100.0 / totalBytes);
            }
        }
    }

    // Best-effort: a mismatch aborts (real corruption/tampering signal), but a
    // missing or unparseable checksums file only warns - it doesn't block the
    // update, since the download itself already came over HTTPS from GitHub.
    private static async Task<bool> VerifyChecksumAsync(
        HttpClient client, Update.GitHubRelease release, string assetName, string zipPath, CancellationToken cancellationToken)
    {
        var checksumAsset = release.Assets.FirstOrDefault(a => string.Equals(a.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
        if (checksumAsset is null)
        {
            AnsiConsole.MarkupLine("[yellow]No SHA256SUMS.txt on this release - skipping checksum verification.[/]");
            return true;
        }

        var checksumContent = await client.GetStringAsync(checksumAsset.DownloadUrl, cancellationToken);
        if (!ChecksumFile.TryGetHash(checksumContent, assetName, out var expectedHash))
        {
            AnsiConsole.MarkupLine("[yellow]Could not find a usable hash for this asset in SHA256SUMS.txt - skipping checksum verification.[/]");
            return true;
        }

        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(zipPath), cancellationToken));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Checksum verification failed - the downloaded file doesn't match SHA256SUMS.txt. Aborting; nothing was changed.[/]");
            File.Delete(zipPath);
            return false;
        }

        return true;
    }
}
