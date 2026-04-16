using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using AegisTune.Core;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;

namespace AegisTune.SystemIntegration;

public sealed class AppUpdateService : IAppUpdateService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<AppUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _currentVersion;
    private readonly AppDistributionKind _distributionKind;
    private AppReleaseNotesState _releaseNotesState = AppReleaseNotesState.CreateInitial();
    private string _releaseNotesCacheKey = string.Empty;

    public AppUpdateService(
        ISettingsStore settingsStore,
        ILogger<AppUpdateService> logger,
        HttpClient? httpClient = null,
        string? currentVersionOverride = null,
        AppDistributionKind? distributionKindOverride = null)
    {
        _settingsStore = settingsStore;
        _logger = logger;
        _httpClient = httpClient ?? CreateDefaultHttpClient();

        (_currentVersion, _distributionKind) = ResolveRuntimeIdentity(currentVersionOverride, distributionKindOverride);
        CurrentState = AppUpdateState.CreateInitial(_currentVersion, _distributionKind);
    }

    public AppUpdateState CurrentState { get; private set; }

    public async Task<AppUpdateState> RefreshAsync(bool respectLaunchPreference, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            AppSettings settings = await _settingsStore.LoadAsync(cancellationToken);
            string feedUrl = settings.EffectiveUpdateManifestUrl;

            if (respectLaunchPreference && !settings.CheckForAppUpdatesOnLaunch)
            {
                CurrentState = new AppUpdateState(
                    _currentVersion,
                    _distributionKind,
                    false,
                    false,
                    false,
                    "Automatic app update checks are turned off.",
                    "Turn on launch checks in Settings or use Check now when you want to query the update feed.",
                    feedUrl);

                return CurrentState;
            }

            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                CurrentState = new AppUpdateState(
                    _currentVersion,
                    _distributionKind,
                    settings.CheckForAppUpdatesOnLaunch,
                    false,
                    false,
                    "No app update feed URL is configured yet.",
                    "Set the update manifest URL in Settings before you publish updates.",
                    string.Empty);

                return CurrentState;
            }

            UpdateManifestDto manifest = await LoadManifestAsync(feedUrl, cancellationToken);
            string latestVersion = NormalizeVersion(manifest.Version!);
            bool updateAvailable = CompareVersions(latestVersion, _currentVersion) > 0;

            string statusLine = updateAvailable
                ? _distributionKind == AppDistributionKind.Packaged
                    ? $"Version {latestVersion} is available for this MSIX install."
                    : $"Version {latestVersion} is available for this portable build."
                : $"This {_distributionKind switch { AppDistributionKind.Packaged => "MSIX install", _ => "portable build" }} is already on the latest version published in the feed.";

            string guidanceLine = updateAvailable
                ? _distributionKind == AppDistributionKind.Packaged
                    ? "Open the App Installer update source so Windows can stage the newer MSIX build."
                    : "Download the newer portable zip and replace the current portable folder after you close the app."
                : "The feed is reachable and no newer version is currently published.";

            CurrentState = new AppUpdateState(
                _currentVersion,
                _distributionKind,
                settings.CheckForAppUpdatesOnLaunch,
                true,
                updateAvailable,
                statusLine,
                guidanceLine,
                feedUrl,
                latestVersion,
                manifest.Portable?.Url,
                manifest.Msix?.Url,
                manifest.Msix?.AppInstallerUrl,
                manifest.NotesUrl,
                DateTimeOffset.Now);

            ResetReleaseNotesCache(manifest.NotesUrl);

            return CurrentState;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App update check failed.");
            CurrentState = new AppUpdateState(
                _currentVersion,
                _distributionKind,
                true,
                true,
                false,
                "The app update feed could not be checked.",
                ex.Message,
                CurrentState.FeedUrl,
                CheckedAt: DateTimeOffset.Now);

            ResetReleaseNotesCache(null);

            return CurrentState;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppReleaseNotesState> GetReleaseNotesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            string releaseNotesUrl = CurrentState.ReleaseNotesUrl ?? string.Empty;
            string title = BuildReleaseNotesTitle();

            if (string.IsNullOrWhiteSpace(releaseNotesUrl))
            {
                _releaseNotesState = new AppReleaseNotesState(
                    true,
                    false,
                    title,
                    "No release notes source is configured in the current update feed.",
                    string.Empty,
                    "Publish a notes URL in stable.json if you want the in-app changelog viewer to work.",
                    DateTimeOffset.Now);

                _releaseNotesCacheKey = string.Empty;
                return _releaseNotesState;
            }

            if (_releaseNotesState.IsAvailable
                && string.Equals(_releaseNotesCacheKey, releaseNotesUrl, StringComparison.OrdinalIgnoreCase))
            {
                return _releaseNotesState;
            }

            string notesContent = (await LoadTextAsync(releaseNotesUrl, cancellationToken)).Trim();
            if (string.IsNullOrWhiteSpace(notesContent))
            {
                notesContent = "The release notes file was reachable, but it did not contain any visible text.";
            }

            _releaseNotesState = new AppReleaseNotesState(
                true,
                true,
                ExtractReleaseNotesTitle(notesContent, title),
                notesContent,
                releaseNotesUrl,
                "Release notes loaded from the published update feed.",
                DateTimeOffset.Now);

            _releaseNotesCacheKey = releaseNotesUrl;
            return _releaseNotesState;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Release notes could not be loaded.");
            _releaseNotesState = new AppReleaseNotesState(
                true,
                false,
                BuildReleaseNotesTitle(),
                $"Release notes could not be loaded.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                CurrentState.ReleaseNotesUrl ?? string.Empty,
                "The published notes file could not be read from the configured source.",
                DateTimeOffset.Now);

            _releaseNotesCacheKey = CurrentState.ReleaseNotesUrl ?? string.Empty;
            return _releaseNotesState;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UpdateManifestDto> LoadManifestAsync(string feedUrl, CancellationToken cancellationToken)
    {
        string json = await LoadTextAsync(feedUrl, cancellationToken);

        UpdateManifestDto? manifest = JsonSerializer.Deserialize<UpdateManifestDto>(json, SerializerOptions);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("The update feed did not include a valid version.");
        }

        return manifest;
    }

    private static (string CurrentVersion, AppDistributionKind DistributionKind) ResolveRuntimeIdentity(
        string? currentVersionOverride,
        AppDistributionKind? distributionKindOverride)
    {
        if (!string.IsNullOrWhiteSpace(currentVersionOverride) && distributionKindOverride is not null)
        {
            return (NormalizeVersion(currentVersionOverride), distributionKindOverride.Value);
        }

        try
        {
            PackageVersion version = Package.Current.Id.Version;
            return ($"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}", AppDistributionKind.Packaged);
        }
        catch
        {
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? typeof(AppUpdateService).GetTypeInfo().Assembly.GetName().Version;

            return (NormalizeVersion(version?.ToString() ?? "0.0.0.0"), AppDistributionKind.Portable);
        }
    }

    private static int CompareVersions(string left, string right)
    {
        Version normalizedLeft = Version.Parse(NormalizeVersion(left));
        Version normalizedRight = Version.Parse(NormalizeVersion(right));
        return normalizedLeft.CompareTo(normalizedRight);
    }

    private static string NormalizeVersion(string value)
    {
        string[] parts = value
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => "0.0.0.0",
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => string.Join('.', parts.Take(4))
        };
    }

    private static bool TryResolveFilePath(string value, out string? filePath)
    {
        filePath = null;

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            if (uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                filePath = uri.LocalPath;
                return true;
            }

            return false;
        }

        if (!Path.IsPathRooted(value))
        {
            return false;
        }

        filePath = value;
        return true;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("AegisTune/1.0");
        return client;
    }

    private async Task<string> LoadTextAsync(string source, CancellationToken cancellationToken)
    {
        if (TryResolveFilePath(source, out string? filePath))
        {
            return await File.ReadAllTextAsync(filePath!, cancellationToken);
        }

        using HttpResponseMessage response = await _httpClient.GetAsync(source, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private void ResetReleaseNotesCache(string? releaseNotesUrl)
    {
        string nextKey = releaseNotesUrl ?? string.Empty;
        if (string.Equals(_releaseNotesCacheKey, nextKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _releaseNotesCacheKey = string.Empty;
        _releaseNotesState = AppReleaseNotesState.CreateInitial();
    }

    private string BuildReleaseNotesTitle()
    {
        string version = string.IsNullOrWhiteSpace(CurrentState.LatestVersion)
            ? CurrentState.CurrentVersion
            : CurrentState.LatestVersion;

        return $"Release notes for {version}";
    }

    private static string ExtractReleaseNotesTitle(string content, string fallbackTitle)
    {
        string? firstHeading = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith('#'));

        if (string.IsNullOrWhiteSpace(firstHeading))
        {
            return fallbackTitle;
        }

        return firstHeading.TrimStart('#', ' ').Trim();
    }

    private sealed record UpdateManifestDto(
        string? Channel,
        string? Version,
        string? PublishedAt,
        string? NotesUrl,
        PortablePackageDto? Portable,
        MsixPackageDto? Msix);

    private sealed record PortablePackageDto(
        string? Url,
        string? Sha256);

    private sealed record MsixPackageDto(
        string? Url,
        string? AppInstallerUrl,
        string? Sha256);
}
