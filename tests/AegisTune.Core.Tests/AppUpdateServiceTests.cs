using System.Net;
using System.Net.Http;
using AegisTune.Core;
using AegisTune.SystemIntegration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AegisTune.Core.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task RefreshAsync_WhenLaunchChecksAreDisabled_ReturnsDisabledStateWithoutNetworkCall()
    {
        FakeSettingsStore settingsStore = new(new AppSettings(
            CheckForAppUpdatesOnLaunch: false,
            UpdateManifestUrl: "https://updates.example.com/stable.json"));

        int callCount = 0;
        HttpClient client = new(new StubMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Portable);

        AppUpdateState state = await service.RefreshAsync(true);

        Assert.False(state.AutomaticChecksEnabled);
        Assert.False(state.HasChecked);
        Assert.False(state.IsUpdateAvailable);
        Assert.Equal(0, callCount);
        Assert.Contains("turned off", state.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_WhenPortableFeedHasNewerVersion_ReturnsPortableDownloadUrl()
    {
        const string json = """
{
  "channel": "stable",
  "version": "1.0.26.0",
  "notesUrl": "https://updates.example.com/release-notes",
  "portable": {
    "url": "https://updates.example.com/AegisTune-1.0.26.0-win-x64-portable.zip",
    "sha256": "abc"
  },
  "msix": {
    "url": "https://updates.example.com/AegisTune.App_1.0.26.0_x64.msix",
    "appInstallerUrl": "https://updates.example.com/AegisTune.appinstaller",
    "sha256": "def"
  }
}
""";

        FakeSettingsStore settingsStore = new(new AppSettings(UpdateManifestUrl: "https://updates.example.com/stable.json"));
        HttpClient client = new(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Portable);

        AppUpdateState state = await service.RefreshAsync(false);

        Assert.True(state.HasChecked);
        Assert.True(state.IsUpdateAvailable);
        Assert.Equal("1.0.26.0", state.LatestVersion);
        Assert.Equal(AppDistributionKind.Portable, state.DistributionKind);
        Assert.Equal("https://updates.example.com/AegisTune-1.0.26.0-win-x64-portable.zip", state.PreferredUpdateUrl);
        Assert.Contains("portable", state.GuidanceLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_WhenPackagedFeedHasNewerVersion_PrefersAppInstallerUrl()
    {
        const string json = """
{
  "channel": "stable",
  "version": "1.0.26.0",
  "portable": {
    "url": "https://updates.example.com/AegisTune-1.0.26.0-win-x64-portable.zip",
    "sha256": "abc"
  },
  "msix": {
    "url": "https://updates.example.com/AegisTune.App_1.0.26.0_x64.msix",
    "appInstallerUrl": "https://updates.example.com/AegisTune.appinstaller",
    "sha256": "def"
  }
}
""";

        FakeSettingsStore settingsStore = new(new AppSettings(UpdateManifestUrl: "https://updates.example.com/stable.json"));
        HttpClient client = new(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Packaged);

        AppUpdateState state = await service.RefreshAsync(false);

        Assert.True(state.IsUpdateAvailable);
        Assert.Equal("https://updates.example.com/AegisTune.appinstaller", state.PreferredUpdateUrl);
        Assert.Contains("MSIX", state.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_WhenPackagedFeedOmitsMsix_FallsBackToFeedUrl()
    {
        const string json = """
{
  "channel": "stable",
  "version": "1.0.26.0",
  "portable": {
    "url": "https://updates.example.com/AegisTune-1.0.26.0-win-x64-portable.zip",
    "sha256": "abc"
  }
}
""";

        FakeSettingsStore settingsStore = new(new AppSettings(UpdateManifestUrl: "https://updates.example.com/stable.json"));
        HttpClient client = new(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Packaged);

        AppUpdateState state = await service.RefreshAsync(false);

        Assert.True(state.IsUpdateAvailable);
        Assert.Equal("https://updates.example.com/stable.json", state.PreferredUpdateUrl);
        Assert.True(string.IsNullOrWhiteSpace(state.AppInstallerUrl));
        Assert.True(string.IsNullOrWhiteSpace(state.MsixPackageUrl));
    }

    [Fact]
    public async Task RefreshAsync_WhenFeedFails_ReturnsErrorState()
    {
        FakeSettingsStore settingsStore = new(new AppSettings(UpdateManifestUrl: "https://updates.example.com/stable.json"));
        HttpClient client = new(new StubMessageHandler(_ => throw new HttpRequestException("Network unreachable.")));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Packaged);

        AppUpdateState state = await service.RefreshAsync(false);

        Assert.True(state.HasChecked);
        Assert.False(state.IsUpdateAvailable);
        Assert.Contains("could not be checked", state.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Network unreachable", state.GuidanceLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReleaseNotesAsync_WhenFeedPublishesNotesUrl_LoadsReleaseNotesContent()
    {
        const string manifestJson = """
{
  "channel": "stable",
  "version": "1.0.26.0",
  "notesUrl": "https://updates.example.com/release-notes.md",
  "portable": {
    "url": "https://updates.example.com/AegisTune-1.0.26.0-win-x64-portable.zip"
  }
}
""";

        const string releaseNotes = """
# AegisTune 1.0.26.0

- Added in-app changelog viewing.
- Kept the update source as the next operator action.
""";

        FakeSettingsStore settingsStore = new(new AppSettings(UpdateManifestUrl: "https://updates.example.com/stable.json"));
        HttpClient client = new(new StubMessageHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri.EndsWith("stable.json", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifestJson)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(releaseNotes)
            };
        }));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Portable);

        await service.RefreshAsync(false);
        AppReleaseNotesState notesState = await service.GetReleaseNotesAsync();

        Assert.True(notesState.HasLoaded);
        Assert.True(notesState.IsAvailable);
        Assert.Equal("AegisTune 1.0.26.0", notesState.Title);
        Assert.Contains("in-app changelog viewing", notesState.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("https://updates.example.com/release-notes.md", notesState.SourceUrl);
    }

    [Fact]
    public async Task GetReleaseNotesAsync_WhenFeedHasNoNotesUrl_ReturnsUnavailableState()
    {
        const string manifestJson = """
{
  "channel": "stable",
  "version": "1.0.26.0",
  "portable": {
    "url": "https://updates.example.com/AegisTune-1.0.26.0-win-x64-portable.zip"
  }
}
""";

        FakeSettingsStore settingsStore = new(new AppSettings(UpdateManifestUrl: "https://updates.example.com/stable.json"));
        HttpClient client = new(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(manifestJson)
        }));

        AppUpdateService service = new(
            settingsStore,
            NullLogger<AppUpdateService>.Instance,
            client,
            currentVersionOverride: "1.0.25.0",
            distributionKindOverride: AppDistributionKind.Portable);

        await service.RefreshAsync(false);
        AppReleaseNotesState notesState = await service.GetReleaseNotesAsync();

        Assert.True(notesState.HasLoaded);
        Assert.False(notesState.IsAvailable);
        Assert.Contains("No release notes source", notesState.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Publish a notes URL", notesState.StatusLine, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly AppSettings _settings;

        public FakeSettingsStore(AppSettings settings)
        {
            _settings = settings;
        }

        public string StoragePath => "memory";

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responseFactory(request));
    }
}
