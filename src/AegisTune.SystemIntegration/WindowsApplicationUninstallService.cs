using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsApplicationUninstallService : IApplicationUninstallService
{
    private static readonly TimeSpan CompletionProbeWindow = TimeSpan.FromSeconds(2);

    private readonly IRiskyChangePreflightService _preflightService;
    private readonly IUndoJournalStore _undoJournalStore;

    public WindowsApplicationUninstallService(
        IRiskyChangePreflightService preflightService,
        IUndoJournalStore undoJournalStore)
    {
        _preflightService = preflightService;
        _undoJournalStore = undoJournalStore;
    }

    public async Task<ApplicationUninstallExecutionResult> UninstallAsync(
        InstalledApplicationRecord application,
        bool dryRunEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        DateTimeOffset processedAt = DateTimeOffset.Now;

        if (!application.CanRunUninstall)
        {
            return new ApplicationUninstallExecutionResult(
                application.DisplayName,
                application.UninstallCommand ?? string.Empty,
                dryRunEnabled,
                false,
                false,
                false,
                null,
                processedAt,
                "This app does not expose a runnable uninstall command.",
                "Use Installed apps or Programs and Features when the registry entry does not provide a trusted uninstall command.");
        }

        if (!CommandPathResolver.TrySplitCommand(application.UninstallCommand, out string fileName, out string arguments))
        {
            return new ApplicationUninstallExecutionResult(
                application.DisplayName,
                application.UninstallCommand ?? string.Empty,
                dryRunEnabled,
                false,
                false,
                false,
                null,
                processedAt,
                $"AegisTune could not parse the uninstall command for {application.DisplayName}.",
                "Copy the uninstall command for manual review or open Installed apps instead.");
        }

        RiskyChangePreflightResult preflight = await _preflightService.PrepareAsync(
            new RiskyChangePreflightRequest(
                RiskyChangeType.ApplicationUninstall,
                $"App uninstall for {application.DisplayName}",
                SystemRestoreIntent.ApplicationInstall),
            dryRunEnabled,
            cancellationToken);

        if (!preflight.ShouldProceed)
        {
            return new ApplicationUninstallExecutionResult(
                application.DisplayName,
                application.UninstallCommand!,
                false,
                false,
                false,
                false,
                null,
                processedAt,
                preflight.StatusLine,
                preflight.GuidanceLine);
        }

        if (dryRunEnabled)
        {
            return new ApplicationUninstallExecutionResult(
                application.DisplayName,
                application.UninstallCommand!,
                true,
                true,
                false,
                false,
                null,
                processedAt,
                $"{preflight.StatusLine} AegisTune previewed the uninstall workflow for {application.DisplayName}.",
                "Disable dry-run in Settings when you are ready to launch the recorded uninstall command.");
        }

        try
        {
            using Process process = StartUninstallProcess(fileName, arguments);

            Task exitTask = process.WaitForExitAsync(cancellationToken);
            Task completedTask = await Task.WhenAny(exitTask, Task.Delay(CompletionProbeWindow, cancellationToken));
            bool completedWithinProbeWindow = ReferenceEquals(completedTask, exitTask);
            int? exitCode = completedWithinProbeWindow ? process.ExitCode : null;

            if (completedWithinProbeWindow && exitCode != 0)
            {
                return new ApplicationUninstallExecutionResult(
                    application.DisplayName,
                    application.UninstallCommand!,
                    false,
                    false,
                    true,
                    true,
                    exitCode,
                    processedAt,
                    $"{preflight.StatusLine} The uninstall command exited early with code {exitCode}.",
                    "Review the uninstall target and retry from Apps & Uninstall or the Windows Installed apps surface.");
            }

            ApplicationUninstallExecutionResult result = new(
                application.DisplayName,
                application.UninstallCommand!,
                false,
                true,
                true,
                completedWithinProbeWindow,
                exitCode,
                processedAt,
                completedWithinProbeWindow
                    ? $"{preflight.StatusLine} The uninstall workflow for {application.DisplayName} completed its initial launcher pass."
                    : $"{preflight.StatusLine} Launched the uninstall workflow for {application.DisplayName}.",
                completedWithinProbeWindow
                    ? "Refresh Apps & Uninstall to confirm whether the app registration or leftover footprint changed."
                    : "Finish the vendor uninstall flow, then refresh Apps & Uninstall and review Safety & Undo for the recorded uninstall history.");

            await _undoJournalStore.AppendAsync(
                new UndoJournalEntry(
                    Guid.NewGuid(),
                    UndoJournalEntryKind.ApplicationUninstall,
                    $"App uninstall: {application.DisplayName}",
                    processedAt,
                    result.StatusLine,
                    result.GuidanceLine,
                    RestorePointCreated: preflight.RestorePointCreated,
                    RestorePointReused: preflight.RestorePointReused,
                    ArtifactPath: application.ResolvedUninstallTargetPath ?? CommandPathResolver.ResolveTargetPath(application.UninstallCommand),
                    TargetDetail: application.RegistryKeyPath,
                    CommandLine: application.UninstallCommand),
                cancellationToken);

            return result;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new ApplicationUninstallExecutionResult(
                application.DisplayName,
                application.UninstallCommand!,
                false,
                false,
                false,
                false,
                null,
                processedAt,
                $"{preflight.StatusLine} The uninstall elevation prompt was canceled before the workflow could start.",
                "Run the uninstall again when you are ready to approve the elevated uninstall workflow.");
        }
        catch (Exception ex)
        {
            return new ApplicationUninstallExecutionResult(
                application.DisplayName,
                application.UninstallCommand!,
                false,
                false,
                false,
                false,
                null,
                processedAt,
                $"The uninstall workflow for {application.DisplayName} could not be started: {ex.Message}",
                "Review the uninstall command and target path before retrying.");
        }
    }

    private static Process StartUninstallProcess(string fileName, string arguments)
    {
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            }) ?? throw new InvalidOperationException($"Failed to start the uninstall workflow for {fileName}.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 740)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            }) ?? throw new InvalidOperationException($"Failed to start the elevated uninstall workflow for {fileName}.");
        }
    }
}
