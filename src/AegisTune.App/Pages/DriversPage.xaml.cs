using AegisTune.Core;
using AegisTune.DriverEngine;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace AegisTune.App.Pages;

public sealed partial class DriversPage : Page
{
    private const double MediumLayoutBreakpoint = 700;
    private const double WideLayoutBreakpoint = 1040;
    private const string NeedsReviewFilter = "Start with problem devices";
    private const string PriorityReviewFilter = "High-risk devices";
    private const string CriticalClassFilter = "Critical hardware classes";
    private const string AllDevicesFilter = "All scanned devices";

    private string? _loadErrorMessage;
    private string? _actionStatusMessage;
    private string _activeFilter = NeedsReviewFilter;
    private string _searchText = string.Empty;
    private bool _isSynchronizingQueueSelection;
    private bool _isSynchronizingRepositorySelection;
    private AppSettings? _settings;

    public DriversPage()
    {
        InitializeComponent();
        Loaded += DriversPage_Loaded;
        SizeChanged += DriversPage_SizeChanged;
    }

    public ModuleSnapshot? Module { get; private set; }

    public FirmwareInventorySnapshot? Firmware { get; private set; }

    public FirmwareReleaseLookupResult? LastFirmwareLookupResult { get; private set; }

    public FirmwareSafetyAssessment? FirmwareSafetyAssessment { get; private set; }

    public DeviceInventorySnapshot? Inventory { get; private set; }

    public DriverAuditExportResult? LastExport { get; private set; }

    public DriverRemediationExportResult? LastPlanExport { get; private set; }

    public DriverDepotScanResult? DepotScan { get; private set; }

    public DriverInstallExecutionResult? LastInstallResult { get; private set; }

    public DriverInstallVerificationResult? LastInstallVerificationResult { get; private set; }

    public DriverStoreDeviceEvidenceResult? LastDriverStoreEvidenceResult { get; private set; }

    public DriverRepositorySeedResult? LastRepositorySeedResult { get; private set; }

    public DriverDeviceRecord? SelectedDevice { get; private set; }

    public DriverRemediationPlan? SelectedRemediationPlan { get; private set; }

    public DriverRepositoryCandidate? SelectedRepositoryCandidate { get; private set; }

    public IReadOnlyList<DriverWorkflowStep> Steps => DriverWorkflowCatalog.All;

    public IReadOnlyList<DriverDeviceRecord> Devices => Inventory?.Devices ?? Array.Empty<DriverDeviceRecord>();

    public IReadOnlyList<DriverDeviceRecord> FilteredDevices => ApplyFilters(Devices);

    public string ModuleSubtitle => Module?.Subtitle ?? "Guided device audit and recommendation surface.";

    public string ModuleStatusLine => _loadErrorMessage ?? Module?.StatusLine ?? "Loading driver workflow posture.";

    public string ActionStatusLine => _actionStatusMessage
        ?? "Start with the device scan. Then either install a matching local driver, open Windows Update, or use the official OEM path shown for the selected device.";

    public string DeviceCountLabel => Inventory?.TotalDeviceCount.ToString("N0") ?? "--";

    public string ReviewCountLabel => Inventory?.NeedsAttentionCount.ToString("N0") ?? "--";

    public string PriorityReviewCountLabel => Inventory?.PriorityReviewCount.ToString("N0") ?? "--";

    public string HealthyCountLabel => Inventory?.HealthyCount.ToString("N0") ?? "--";

    public string UnsignedCountLabel => Inventory?.UnsignedDriverCount.ToString("N0") ?? "--";

    public string HighConfidenceOemCountLabel => Inventory?.HighConfidenceOemCandidateCount.ToString("N0") ?? "--";

    public string HardwareBackedReviewCountLabel => Inventory?.HardwareBackedReviewCount.ToString("N0") ?? "--";

    public string CompatibleFallbackCountLabel => Inventory?.CompatibleFallbackReviewCount.ToString("N0") ?? "--";

    public string NoIdentifierReviewCountLabel => Inventory?.NoIdentifierReviewCount.ToString("N0") ?? "--";

    public string GenericProviderCountLabel => Inventory?.GenericProviderReviewCount.ToString("N0") ?? "--";

    public string FirmwareSupportIdentityLabel => Firmware?.SupportIdentityLabel ?? "Firmware identity unavailable";

    public string FirmwareSupportSourceLabel => Firmware?.SupportIdentitySourceLabel ?? "Firmware identity source unavailable";

    public string FirmwareSystemIdentityLabel => Firmware?.SystemIdentityLabel ?? "System identity unavailable";

    public string FirmwareBoardIdentityLabel => Firmware?.BoardIdentityLabel ?? "Baseboard identity unavailable";

    public string FirmwareBiosVendorLabel => Firmware?.BiosManufacturerLabel ?? "BIOS vendor unknown";

    public string FirmwareBiosVersionLabel => Firmware?.BiosVersionLabel ?? "BIOS version unknown";

    public string FirmwareFamilyVersionLabel => Firmware?.BiosFamilyVersionLabel ?? "Firmware family version unavailable";

    public string FirmwareReleaseDateLabel => Firmware?.BiosReleaseDateLabel ?? "Release date unknown";

    public string FirmwareAgeLabel => Firmware?.BiosAgeLabel ?? "BIOS age unknown";

    public string FirmwareModeLabel => Firmware?.FirmwareModeLabel ?? "Firmware mode unknown";

    public string FirmwareSecureBootLabel => Firmware?.SecureBootLabel ?? "Secure Boot unknown";

    public string FirmwareSecurityPostureLabel => Firmware?.SecurityPostureLabel ?? "Firmware security posture unavailable";

    public string FirmwareRouteLabel => Firmware?.SupportRouteLabel ?? "Firmware route unavailable";

    public string FirmwarePrimarySupportUrlLabel => Firmware?.PrimarySupportUrlLabel ?? "Official support route unavailable";

    public string FirmwareSearchHintLabel => Firmware?.SupportSearchHint ?? "Support search hint unavailable";

    public string FirmwareReadinessSummary => Firmware?.DashboardStatusLine ?? "Firmware inventory is still loading.";

    public string FirmwareSupportOptionsPreview => Firmware?.SupportOptionsPreview ?? "No firmware support steps generated yet.";

    public string FirmwareCollectedAtLabel => Firmware is null
        ? "No firmware snapshot timestamp is available yet."
        : $"Collected at {Firmware.CollectedAt.ToLocalTime():g}.";

    public string FirmwareBrief => Firmware?.FirmwareBrief ?? "Firmware inventory unavailable.";

    public string FirmwareLookupStatusLine => LastFirmwareLookupResult?.StatusLine
        ?? "Latest BIOS lookup has not been run yet. Use the on-demand check so the app stays explicit about network evidence.";

    public string FirmwareLookupGuidanceLine => LastFirmwareLookupResult?.GuidanceLine
        ?? "The app will only claim a latest BIOS version where the official vendor source is deterministic. Otherwise it will keep you on the official support or vendor-tool path.";

    public string FirmwareLookupModeLabel => LastFirmwareLookupResult?.ModeLabel ?? "Not checked";

    public string FirmwareLatestVersionLabel => LastFirmwareLookupResult?.LatestVersionLabel ?? "Latest BIOS version not verified yet";

    public string FirmwareLatestReleaseDateLabel => LastFirmwareLookupResult?.LatestReleaseDateLabel ?? "Latest release date not verified yet";

    public string FirmwareLookupComparisonSummary => LastFirmwareLookupResult?.ComparisonSummary
        ?? "Checks whether your current BIOS matches the latest version from an official vendor source when that source can be verified.";

    public string FirmwareLookupEvidenceSourceLabel => LastFirmwareLookupResult?.EvidenceSourceLabel ?? "Evidence source unavailable";

    public string FirmwareLookupSearchHintLabel => LastFirmwareLookupResult?.SearchHint ?? FirmwareSearchHintLabel;

    public string FirmwareLookupSupportUrlLabel => LastFirmwareLookupResult?.SupportUrlLabel ?? FirmwarePrimarySupportUrlLabel;

    public string FirmwareLookupDetailsUrlLabel => LastFirmwareLookupResult?.DetailsUrlLabel ?? FirmwarePrimarySupportUrlLabel;

    public string FirmwareLookupToolTitleLabel => LastFirmwareLookupResult?.ToolTitleLabel ?? "No vendor utility workflow attached";

    public string FirmwareLookupToolDetailLabel => LastFirmwareLookupResult?.ToolDetailLabel ?? "No vendor utility detail is attached to the current lookup result.";

    public string FirmwareLookupCheckedAtLabel => LastFirmwareLookupResult?.CheckedAtLabel ?? "Lookup not run yet";

    public string FirmwareFlashTargetSummary => CurrentFirmwareFlashPreparationGuide.TargetSummary;

    public string FirmwareFlashReleaseNotesSummary => CurrentFirmwareFlashPreparationGuide.ReleaseNotesSummary;

    public string FirmwareFlashReleaseNotesPreview => CurrentFirmwareFlashPreparationGuide.ReleaseNotesPreview;

    public string FirmwareFlashCommandPreview => CurrentFirmwareFlashPreparationGuide.CommandPreview;

    public string FirmwareSafetyOverallPostureLabel => FirmwareSafetyAssessment?.OverallPostureLabel ?? "Firmware safety assessment not available yet";

    public string FirmwareSafetySummaryLine => FirmwareSafetyAssessment?.SummaryLine
        ?? "Safety gates have not been assessed yet. Refresh the page or run the latest BIOS check to rebuild the flash posture.";

    public string FirmwareSafetyBitLockerStatusLine => FirmwareSafetyAssessment?.BitLockerStatusLine ?? "BitLocker posture unavailable";

    public string FirmwareSafetyPowerStatusLine => FirmwareSafetyAssessment?.PowerStatusLine ?? "Power posture unavailable";

    public string FirmwareSafetySystemDriveLabel => FirmwareSafetyAssessment?.SystemDrive ?? "System drive unknown";

    public string FirmwareSafetyCollectedAtLabel => FirmwareSafetyAssessment is null
        ? "Safety assessment not run yet"
        : $"Assessed at {FirmwareSafetyAssessment.CollectedAt.ToLocalTime():g}.";

    public string FirmwareSafetyBlockingGateCountLabel => FirmwareSafetyAssessment?.BlockingGateCount.ToString("N0") ?? "--";

    public string FirmwareSafetyAttentionGateCountLabel => FirmwareSafetyAssessment?.AttentionGateCount.ToString("N0") ?? "--";

    public string FirmwareSafetyBlockingGateSummary => $"Blocking: {FirmwareSafetyBlockingGateCountLabel}";

    public string FirmwareSafetyAttentionGateSummary => $"Needs attention: {FirmwareSafetyAttentionGateCountLabel}";

    public IReadOnlyList<FirmwareSafetyGate> FirmwareSafetyGates => FirmwareSafetyAssessment?.Gates ?? Array.Empty<FirmwareSafetyGate>();

    public string FilterSummaryLabel => Inventory is null
        ? "Collecting device inventory."
        : $"{FilteredDevices.Count:N0} of {Inventory.TotalDeviceCount:N0} devices shown in {_activeFilter} mode.";

    public string QueueCapStatusLabel
    {
        get
        {
            if (Inventory is null)
            {
                return "The queue is building from the current scan.";
            }

            int totalMatches = ApplyFilterCriteria(Devices).Count();
            return totalMatches > 64
                ? $"Showing the first 64 of {totalMatches:N0} matching devices."
                : "The queue is showing all matching devices from this scan.";
        }
    }

    public string SearchStatusLabel => string.IsNullOrWhiteSpace(_searchText)
        ? "Search is not restricting the queue."
        : $"Queue filtered by '{_searchText}'.";

    public string QueueSelectionHeadline => SelectedDevice is null
        ? "Choose a device to review"
        : $"Selected now: {SelectedDevice.FriendlyName}";

    public string QueueSelectionStatusLine => SelectedDevice is null
        ? "After the scan, click one device card in the queue. That selection controls the detail panel and all device-specific actions."
        : $"{SelectedDeviceReviewBucket} • {SelectedDeviceHealth} • {SelectedDeviceMatchConfidence}";

    public string QueueSelectionWorkflowLine => SelectedDevice is null
        ? "1. Pick a device. 2. Review the selected-device panel. 3. Use the recommended fix path or a vetted local INF match."
        : $"Next suggested path: {SelectedPlanSourceLabel}. {SelectedDeviceActionPlan}";

    public string QueueSelectionCandidateLine => SelectedDevice is null
        ? "Device-specific install and export actions stay disabled until a queue selection exists."
        : HasSelectedRepositoryCandidate
            ? $"{SelectedRepositoryCandidateCountLabel} are ready for this selected device. Use Local driver install only if that vetted INF is the intended path."
            : "No vetted local INF candidate is selected for this device yet. Stay on the recommended review path or add repository roots in Settings.";

    public string QueueSelectionIssueLabel => SelectedDevice?.HealthLabel ?? "Choose one device in the queue first.";

    public string QueueSelectionNextLaneLabel => SelectedDevice is null
        ? "Pick a device to load the recommended fix lane."
        : CurrentSelectedDriverNextAction.Headline;

    public string QueueSelectionCandidateCompactLine => SelectedDevice is null
        ? "Device-specific install stays locked until a queue selection exists."
        : HasSelectedRepositoryCandidate
            ? $"{SelectedRepositoryCandidateCountLabel} ready for this selected device."
            : "No vetted local INF selected yet.";

    public string QueueSelectionCompactWorkflowLine => SelectedDevice is null
        ? "The queue drives the selected-device review and next-step actions."
        : SelectedDeviceActionPlan;

    public string PriorityReviewSummary
    {
        get
        {
            if (Inventory is null)
            {
                return "Collecting priority-review devices.";
            }

            DriverDeviceRecord[] devices = Inventory.Devices
                .Where(device => device.RequiresPriorityReview)
                .Take(6)
                .ToArray();

            if (devices.Length == 0)
            {
                return "No priority-review devices are currently flagged in this scan.";
            }

            return string.Join(
                Environment.NewLine,
                devices.Select((device, index) => $"{index + 1}. {device.FriendlyName} - {device.ReviewCategory}"));
        }
    }

    public string SelectedDeviceName => SelectedDevice?.FriendlyName ?? "Select a device from the review queue.";

    public string SelectedDeviceReviewBucket => SelectedDevice?.ReviewBucketLabel ?? "No review bucket available.";

    public string SelectedDeviceHealth => SelectedDevice?.HealthLabel ?? "Health signal unavailable.";

    public string SelectedDeviceCategory => SelectedDevice?.ReviewCategory ?? "No review category available.";

    public string SelectedDeviceSummaryLine => SelectedDevice is null
        ? "No review bucket available. Health signal unavailable. No review category available."
        : $"{SelectedDeviceReviewBucket} • {SelectedDeviceHealth} • {SelectedDeviceCategory}";

    public string SelectedDriverNextActionHeadline => CurrentSelectedDriverNextAction.Headline;

    public string SelectedDriverNextActionSummary => CurrentSelectedDriverNextAction.Summary;

    public string SelectedDriverNextActionNextStep => CurrentSelectedDriverNextAction.NextStep;

    public string SelectedPrimaryDriverActionLabel => CurrentSelectedDriverNextAction.PrimaryActionLabel;

    public string SelectedSecondaryDriverActionLabel => CurrentSelectedDriverNextAction.SecondaryActionLabel;

    public Visibility SelectedPrimaryDriverActionVisibility => CurrentSelectedDriverNextAction.HasPrimaryAction ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectedSecondaryDriverActionVisibility => CurrentSelectedDriverNextAction.HasSecondaryAction ? Visibility.Visible : Visibility.Collapsed;

    public string SelectedDeviceClass => SelectedDevice?.DeviceClass ?? "Class unavailable";

    public string SelectedDeviceManufacturer => SelectedDevice?.Manufacturer ?? "Manufacturer unavailable";

    public string SelectedDeviceProvider => SelectedDevice?.ProviderLabel ?? "Provider unavailable";

    public string SelectedDeviceVersion => SelectedDevice?.VersionLabel ?? "Version unavailable";

    public string SelectedDeviceDate => SelectedDevice?.DriverDateLabel ?? "Date unavailable";

    public string SelectedDeviceSigning => SelectedDevice?.SigningLabel ?? "Signing unavailable";

    public string SelectedDeviceSigner => SelectedDevice?.SignerLabel ?? "Signer unavailable";

    public string SelectedDeviceService => SelectedDevice?.ServiceLabel ?? "Service unavailable";

    public string SelectedDevicePresence => SelectedDevice?.PresenceLabel ?? "Presence unavailable";

    public string SelectedDeviceClassGuid => SelectedDevice?.ClassGuidLabel ?? "Class GUID unavailable";

    public string SelectedDeviceInf => SelectedDevice?.InfLabel ?? "INF file unavailable";

    public string SelectedDevicePriorityContext => SelectedDevice?.ClassPriorityLabel ?? "Priority context unavailable";

    public string SelectedDeviceInstanceId => SelectedDevice?.InstanceId ?? "Instance ID unavailable";

    public string SelectedDeviceHardwareIdCount => SelectedDevice?.HardwareIdCountLabel ?? "Hardware IDs unavailable";

    public string SelectedDevicePrimaryHardwareId => SelectedDevice?.PrimaryHardwareId ?? "Hardware ID unavailable";

    public string SelectedDeviceHardwareIds => SelectedDevice?.HardwareIdsPreview ?? "No hardware IDs reported by Windows for this device.";

    public string SelectedDeviceCompatibleIdCount => SelectedDevice?.CompatibleIdCountLabel ?? "Compatible IDs unavailable";

    public string SelectedDevicePrimaryCompatibleId => SelectedDevice?.PrimaryCompatibleId ?? "Compatible ID unavailable";

    public string SelectedDeviceCompatibleIds => SelectedDevice?.CompatibleIdsPreview ?? "No compatible IDs reported by Windows for this device.";

    public string SelectedDeviceEvidenceTier => SelectedDevice?.EvidenceTierLabel ?? "No identifier evidence";

    public string SelectedDeviceEvidenceTierDescription => SelectedDevice?.EvidenceTierDescription ?? "Select a device to inspect identifier evidence quality.";

    public string SelectedDeviceMatchConfidence => SelectedDevice?.MatchConfidenceLabel ?? "OEM match unknown";

    public string SelectedDeviceMatchConfidenceReason => SelectedDevice?.MatchConfidenceReason ?? "Select a device to inspect OEM-match confidence.";

    public string SelectedDeviceActionPlan => SelectedDevice?.RecommendedAction
        ?? "Select a device to see the recommended review path.";

    public string SelectedDeviceTechnicianHandoff => SelectedDevice?.TechnicianHandoffSummary
        ?? "Select a device to generate a technician-facing handoff summary.";

    public string SelectedPlanSummary => SelectedRemediationPlan?.Summary
        ?? "Select a device to build a structured remediation plan.";

    public string SelectedPlanSourceLabel => SelectedRemediationPlan?.SourceLabel ?? "No remediation source selected.";

    public string SelectedPlanSourceReason => SelectedRemediationPlan?.SourceReason ?? "Select a device to see why this remediation path is preferred.";

    public string SelectedPlanRollbackLabel => SelectedRemediationPlan?.RollbackLabel ?? "No rollback posture available.";

    public string SelectedPlanRollbackDetail => SelectedRemediationPlan?.RollbackDetail ?? "Select a device to see rollback preparation guidance.";

    public string SelectedPlanRebootLabel => SelectedRemediationPlan?.RebootGuidanceLabel ?? "Reboot posture unavailable.";

    public string SelectedPlanRebootDetail => SelectedRemediationPlan?.RebootGuidanceDetail ?? "Select a device to see reboot verification guidance.";

    public string SelectedPlanVerificationStatus => SelectedRemediationPlan?.VerificationStatusLine ?? "Select a device to queue verification steps.";

    public IReadOnlyList<DriverVerificationStep> SelectedPlanVerificationSteps => SelectedRemediationPlan?.VerificationSteps
        ?? Array.Empty<DriverVerificationStep>();

    public IReadOnlyList<DriverRepositoryCandidate> RepositoryCandidates => DepotScan?.GetCandidates(SelectedDevice?.InstanceId)
        ?? Array.Empty<DriverRepositoryCandidate>();

    public string RepositoryStatusLine => DepotScan?.StatusLine
        ?? "Repository scan not started yet.";

    public string RepositoryRootSummaryLabel => DepotScan?.RootSummaryLabel
        ?? "Driver repositories not loaded yet.";

    public string RepositoryPackageCountLabel => DepotScan is null
        ? "--"
        : DepotScan.PackageCount.ToString("N0");

    public string SelectedRepositoryCandidateCountLabel => RepositoryCandidates.Count == 0
        ? "No local candidates"
        : $"{RepositoryCandidates.Count:N0} local candidate{(RepositoryCandidates.Count == 1 ? string.Empty : "s")}";

    public string SelectedRepositoryCandidateName => SelectedRepositoryCandidate?.FileName ?? "Select a local INF candidate to inspect install readiness.";

    public string SelectedRepositoryCandidateSummary => SelectedRepositoryCandidate?.SummaryLine
        ?? "No local repository candidate is currently selected for this device.";

    public string SelectedRepositoryCandidateMatchLabel => SelectedRepositoryCandidate?.MatchKindLabel ?? "No local match";

    public string SelectedRepositoryCandidateMatchDetail => SelectedRepositoryCandidate?.MatchDetail
        ?? "Add vetted local driver repositories in Settings and refresh the Driver Center to build local INF matches.";

    public string SelectedRepositoryCandidateProvider => SelectedRepositoryCandidate?.ProviderLabel ?? "Provider unavailable";

    public string SelectedRepositoryCandidateClass => SelectedRepositoryCandidate?.ClassLabel ?? "Class unavailable";

    public string SelectedRepositoryCandidateVersion => SelectedRepositoryCandidate?.VersionLabel ?? "Version unavailable";

    public string SelectedRepositoryCandidateCatalog => SelectedRepositoryCandidate?.CatalogLabel ?? "Catalog unavailable";

    public string SelectedRepositoryCandidatePath => SelectedRepositoryCandidate?.InfPath ?? "INF path unavailable";

    public string SelectedRepositoryRootPath => SelectedRepositoryCandidate?.RepositoryRoot ?? "Repository root unavailable";

    public string SelectedRepositoryMatchedIdentifierCount => SelectedRepositoryCandidate?.MatchedIdentifierCountLabel ?? "No matched identifiers";

    public string SelectedRepositoryMatchedIdentifiers => SelectedRepositoryCandidate?.MatchedIdentifiersPreview
        ?? "No matched identifiers are available for the selected local candidate.";

    public string InstallReadinessHeadline => SelectedRepositoryCandidate is null
        ? "No matching local driver is selected yet."
        : $"Ready to use {SelectedRepositoryCandidateName}";

    public string InstallReadinessDetail => SelectedRepositoryCandidate is null
        ? "Scan devices first. If a vetted local INF match is found, AegisTune can run pnputil for that driver automatically. Otherwise stay on Windows Update or the official OEM route."
        : $"{SelectedRepositoryCandidateSummary} for {SelectedDeviceName}. {(_settings?.CreateRestorePointBeforeFixes == true ? "A live install will require restore-point preflight first." : "Restore-point preflight is currently disabled in Settings.")}";

    public string InstallModeLabel => _settings?.DryRunEnabled == false
        ? _settings?.CreateRestorePointBeforeFixes == true
            ? "Live install mode with restore-point preflight"
            : "Live install mode without restore-point preflight"
        : "Dry-run install mode";

    public string InstallStatusLine => LastInstallResult?.StatusLine
        ?? "Select a local driver candidate to preview or execute a pnputil install.";

    public string InstallVerificationHint => LastInstallResult?.VerificationHint
        ?? "AegisTune will re-audit the device after a successful live install so the technician can confirm provider, version, INF, and device status.";

    public string InstallVerificationStatusLine => LastInstallVerificationResult?.Summary
        ?? "No post-install verification has been captured yet.";

    public string InstallVerificationOutcomeLabel => LastInstallVerificationResult?.OutcomeLabel
        ?? "Verification pending";

    public string InstallVerificationChangedFieldsLabel => LastInstallVerificationResult?.ChangedFieldsLabel
        ?? "Run a successful live install to compare before and after driver state.";

    public string InstallVerificationFingerprintLine => LastInstallVerificationResult is null
        ? "AegisTune compares provider, version, INF, status, signing, signer, and problem code after the re-audit."
        : $"Provider {LastInstallVerificationResult.BeforeProvider} -> {LastInstallVerificationResult.AfterProvider}; "
            + $"Version {LastInstallVerificationResult.BeforeVersion} -> {LastInstallVerificationResult.AfterVersion}; "
            + $"INF {LastInstallVerificationResult.BeforeInf} -> {LastInstallVerificationResult.AfterInf}.";

    public string InstallVerificationHealthLine => LastInstallVerificationResult is null
        ? "No health transition has been recorded yet."
        : $"Status {LastInstallVerificationResult.BeforeStatus} -> {LastInstallVerificationResult.AfterStatus}; "
            + $"Problem code {LastInstallVerificationResult.BeforeProblemCode} -> {LastInstallVerificationResult.AfterProblemCode}.";

    public string InstallVerificationRecordedAtLabel => LastInstallVerificationResult is null
        ? "No post-install verification timestamp is available yet."
        : $"Verified at {LastInstallVerificationResult.VerifiedAtLabel}.";

    public string InstallVerificationNotesLabel => LastInstallVerificationResult?.Notes
        ?? "Use a successful live install to capture before and after evidence for the selected device.";

    public string DriverStoreEvidenceStatusLine => ActiveDriverStoreEvidenceResult?.StatusLine
        ?? "No driver-store evidence has been captured for the selected device yet.";

    public string DriverStoreEvidenceGuidanceLine => ActiveDriverStoreEvidenceResult?.GuidanceLine
        ?? "Run a live install or refresh the driver-store evidence to inspect installed and outranked packages from pnputil.";

    public string DriverStoreReportedDriverLabel => ActiveDriverStoreEvidenceResult?.ReportedDriverLabel
        ?? "Reported installed driver unavailable";

    public string DriverStoreInstalledDriverLabel => ActiveDriverStoreEvidenceResult?.InstalledDriverSummary
        ?? "No installed driver-store candidate identified yet.";

    public string DriverStoreBestRankedDriverLabel => ActiveDriverStoreEvidenceResult?.BestRankedInstalledDriverSummary
        ?? "No best-ranked installed candidate identified yet.";

    public string DriverStoreMatchCountLabel => ActiveDriverStoreEvidenceResult?.MatchingDriverCountLabel ?? "--";

    public string DriverStoreOutrankedCountLabel => ActiveDriverStoreEvidenceResult?.OutrankedDriverCountLabel ?? "--";

    public string DriverStoreDeviceStatusLabel => ActiveDriverStoreEvidenceResult?.DeviceStatusLabel
        ?? "PnPUtil device status unavailable";

    public string DriverStoreEvidenceCollectedAtLabel => ActiveDriverStoreEvidenceResult is null
        ? "No driver-store evidence timestamp is available yet."
        : $"Collected at {ActiveDriverStoreEvidenceResult.CollectedAtLabel}.";

    public string DriverStoreEvidencePreview => ActiveDriverStoreEvidenceResult?.MatchingDriversPreview
        ?? "No matching driver-store evidence preview is available yet.";

    public string PrimaryRepositoryRootLabel => DepotScan?.ActiveRoots.FirstOrDefault()
        ?? "No active repository root is currently available.";

    public string InstalledPackageSeedReadinessLabel
    {
        get
        {
            if (SelectedDevice is null)
            {
                return "Select a device to inspect whether its installed package can seed the local repository.";
            }

            if (!PnpUtilDriverRepositorySeedService.CanExportInstalledPackage(SelectedDevice))
            {
                return $"Installed package {SelectedDevice.InfLabel} is not an exportable third-party OEM package.";
            }

            return $"Installed package {SelectedDevice.InfName} can be exported into the local repository.";
        }
    }

    public string RepositorySeedStatusLine => LastRepositorySeedResult?.StatusLine
        ?? "No installed package has been exported into the local repository yet.";

    public string RepositorySeedGuidanceLine => LastRepositorySeedResult?.GuidanceLine
        ?? "When the selected device uses an installed OEM package, AegisTune can export it into the local repository to seed future technician installs.";

    public string RepositorySeedExportDirectoryLabel => LastRepositorySeedResult?.ExportDirectory
        ?? "No repository seed export directory created yet.";

    public string ExportStatusLine => LastExport is null
        ? "Driver audit export not created yet."
        : $"Last driver audit export completed at {LastExport.ExportedAtLabel}.";

    public string ExportDirectoryLabel => LastExport?.ExportDirectory ?? "No driver audit export folder created yet.";

    public string ExportHandoffPathLabel => LastExport?.HandoffPath ?? "No priority handoff export created yet.";

    public string ExportRemediationBundlePathLabel => LastExport?.RemediationBundlePath ?? "No remediation bundle export created yet.";

    public string ExportRemediationPlansDirectoryLabel => LastExport?.RemediationPlansDirectory ?? "No remediation plans directory created yet.";

    public string PlanExportStatusLine => LastPlanExport is null
        ? "Selected remediation plan export not created yet."
        : $"Last selected remediation plan export completed at {LastPlanExport.ExportedAtLabel}.";

    public string PlanExportDirectoryLabel => LastPlanExport?.ExportDirectory ?? "No remediation plan export folder created yet.";

    public string PlanMarkdownPathLabel => LastPlanExport?.MarkdownPath ?? "No remediation plan Markdown export created yet.";

    public bool HasSelectedDevice => SelectedDevice is not null;

    public bool HasExportDirectory => LastExport is not null;

    public bool HasPlanExportDirectory => LastPlanExport is not null;

    public bool HasFirmwareSnapshot => Firmware is not null;

    public bool HasFirmwareSupportUrl => Firmware?.HasPrimarySupportUrl == true;

    public bool HasFirmwareLookupResult => LastFirmwareLookupResult is not null;

    public bool HasFirmwareSafetyAssessment => FirmwareSafetyAssessment is not null;

    public bool HasFirmwareLookupTargetUrl => LastFirmwareLookupResult?.HasDetailsUrl == true
        || LastFirmwareLookupResult?.HasSupportUrl == true
        || HasFirmwareSupportUrl;

    public bool HasFirmwareFlashPreparation => HasFirmwareSnapshot || HasFirmwareLookupResult || HasFirmwareSafetyAssessment;

    public bool HasSelectedHardwareIds => SelectedDevice?.HasHardwareIds == true;

    public bool HasSelectedCompatibleIds => SelectedDevice?.HasCompatibleIds == true;

    public bool HasSelectedRepositoryCandidate => SelectedRepositoryCandidate is not null;

    public bool HasActiveRepositoryRoot => DepotScan?.ActiveRoots.Count > 0;

    public bool HasActiveDriverStoreEvidence => ActiveDriverStoreEvidenceResult is not null;

    public bool CanSeedInstalledPackage => HasSelectedDevice
        && HasActiveRepositoryRoot
        && PnpUtilDriverRepositorySeedService.CanExportInstalledPackage(SelectedDevice);

    private DriverReviewNextActionGuidance CurrentSelectedDriverNextAction =>
        DriverReviewNextActionAdvisor.Create(SelectedDevice, SelectedRepositoryCandidate, HasActiveRepositoryRoot);

    private DriverStoreDeviceEvidenceResult? ActiveDriverStoreEvidenceResult =>
        LastDriverStoreEvidenceResult is not null
        && SelectedDevice is not null
        && string.Equals(LastDriverStoreEvidenceResult.DeviceInstanceId, SelectedDevice.InstanceId, StringComparison.OrdinalIgnoreCase)
            ? LastDriverStoreEvidenceResult
            : null;

    private void RefreshVisualState()
    {
        ApplyAdaptiveLayout(ActualWidth);
        RefreshCommandState();
    }

    private void RefreshCommandState()
    {
        if (DriverQueueList is not null)
        {
            DriverQueueList.IsEnabled = FilteredDevices.Count > 0;
        }

        if (RepositoryCandidateList is not null)
        {
            RepositoryCandidateList.IsEnabled = RepositoryCandidates.Count > 0;
        }

        if (OpenAuditFolderButton is not null)
        {
            OpenAuditFolderButton.IsEnabled = HasExportDirectory;
        }

        if (CheckLatestBiosButton is not null)
        {
            CheckLatestBiosButton.IsEnabled = HasFirmwareSnapshot;
        }

        if (OpenFirmwareSupportButton is not null)
        {
            OpenFirmwareSupportButton.IsEnabled = HasFirmwareLookupTargetUrl;
        }

        if (OpenSystemInformationButton is not null)
        {
            OpenSystemInformationButton.IsEnabled = true;
        }

        if (CopyFirmwareBriefButton is not null)
        {
            CopyFirmwareBriefButton.IsEnabled = HasFirmwareSnapshot || HasFirmwareLookupResult || HasFirmwareSafetyAssessment;
        }

        if (CopyFlashPreparationButton is not null)
        {
            CopyFlashPreparationButton.IsEnabled = HasFirmwareFlashPreparation;
        }

        if (OpenPlanFolderButton is not null)
        {
            OpenPlanFolderButton.IsEnabled = HasPlanExportDirectory;
        }

        if (OpenSelectedInfButton is not null)
        {
            OpenSelectedInfButton.IsEnabled = HasSelectedDevice;
        }

        if (CopyInstanceIdButton is not null)
        {
            CopyInstanceIdButton.IsEnabled = HasSelectedDevice;
        }

        if (CopyHardwareIdsButton is not null)
        {
            CopyHardwareIdsButton.IsEnabled = HasSelectedHardwareIds;
        }

        if (CopyCompatibleIdsButton is not null)
        {
            CopyCompatibleIdsButton.IsEnabled = HasSelectedCompatibleIds;
        }

        if (CopyTechnicianBriefButton is not null)
        {
            CopyTechnicianBriefButton.IsEnabled = HasSelectedDevice;
        }

        if (CopyRemediationPlanButton is not null)
        {
            CopyRemediationPlanButton.IsEnabled = HasSelectedDevice;
        }

        if (CopyVerificationChecklistButton is not null)
        {
            CopyVerificationChecklistButton.IsEnabled = HasSelectedDevice;
        }

        if (ExportRemediationPlanButton is not null)
        {
            ExportRemediationPlanButton.IsEnabled = HasSelectedDevice;
        }

        if (InstallRepositoryCandidateButton is not null)
        {
            InstallRepositoryCandidateButton.IsEnabled = HasSelectedRepositoryCandidate;
        }

        if (OpenRepositoryCandidateButton is not null)
        {
            OpenRepositoryCandidateButton.IsEnabled = HasSelectedRepositoryCandidate;
        }

        if (CopyRepositoryCandidatePathButton is not null)
        {
            CopyRepositoryCandidatePathButton.IsEnabled = HasSelectedRepositoryCandidate;
        }

        if (OpenRepositoryRootButton is not null)
        {
            OpenRepositoryRootButton.IsEnabled = HasSelectedRepositoryCandidate;
        }

        if (RefreshDriverStoreEvidenceButton is not null)
        {
            RefreshDriverStoreEvidenceButton.IsEnabled = HasSelectedDevice;
        }

        if (CopyDriverStoreEvidenceButton is not null)
        {
            CopyDriverStoreEvidenceButton.IsEnabled = HasActiveDriverStoreEvidence;
        }

        if (SeedInstalledPackageButton is not null)
        {
            SeedInstalledPackageButton.IsEnabled = CanSeedInstalledPackage;
        }

        if (OpenSeedExportFolderButton is not null)
        {
            OpenSeedExportFolderButton.IsEnabled = LastRepositorySeedResult is not null;
        }
    }

    private void ApplyQueueSource()
    {
        if (DriverQueueList is null)
        {
            return;
        }

        _isSynchronizingQueueSelection = true;
        try
        {
            DriverQueueList.ItemsSource = FilteredDevices;
            DriverQueueList.SelectedItem = SelectedDevice;
        }
        finally
        {
            _isSynchronizingQueueSelection = false;
        }
    }

    private void ApplyRepositoryCandidateSource()
    {
        if (RepositoryCandidateList is null)
        {
            return;
        }

        _isSynchronizingRepositorySelection = true;
        try
        {
            RepositoryCandidateList.ItemsSource = RepositoryCandidates;
            RepositoryCandidateList.SelectedItem = SelectedRepositoryCandidate;
        }
        finally
        {
            _isSynchronizingRepositorySelection = false;
        }
    }

    private async void DriversPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyAdaptiveLayout(ActualWidth);

        if (Module is not null && Inventory is not null)
        {
            return;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            Task<DashboardSnapshot> snapshotTask = App.GetService<IDashboardSnapshotService>().GetSnapshotAsync();
            Task<DeviceInventorySnapshot> inventoryTask = App.GetService<IDeviceInventoryService>().GetSnapshotAsync();
            Task<AppSettings> settingsTask = App.GetService<ISettingsStore>().LoadAsync();

            DashboardSnapshot snapshot = await snapshotTask;
            Inventory = await inventoryTask;
            _settings = await settingsTask;
            Firmware = snapshot.Firmware;
            Module = snapshot.GetModule(AppSection.Drivers);
            SyncFirmwareLookupState();
            await RefreshFirmwareSafetyAssessmentAsync();

            DepotScan = Inventory.Devices.Count == 0
                ? new DriverDepotScanResult(
                    _settings.DriverRepositoryRoots,
                    Array.Empty<string>(),
                    0,
                    new Dictionary<string, IReadOnlyList<DriverRepositoryCandidate>>(StringComparer.OrdinalIgnoreCase),
                    DateTimeOffset.Now,
                    "Device inventory returned no devices, so the local repository scan was skipped.")
                : await App.GetService<IDriverDepotService>().ScanAsync(_settings.DriverRepositoryRoots, Inventory.Devices);

            EnsureSelectedDevice();
            UpdateSelectedRemediationPlan();
            UpdateSelectedRepositoryCandidate();
        }
        catch (Exception ex)
        {
            _loadErrorMessage = "The driver inventory could not be completed.";
            Firmware = null;
            FirmwareSafetyAssessment = null;
            DepotScan = null;
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Drivers page failed to load.");
        }

        Bindings.Update();
        ApplyQueueSource();
        ApplyRepositoryCandidateSource();
        RefreshVisualState();
    }

    private IEnumerable<DriverDeviceRecord> ApplyFilterCriteria(IEnumerable<DriverDeviceRecord> devices)
    {
        IEnumerable<DriverDeviceRecord> filtered = devices;

        filtered = _activeFilter switch
        {
            PriorityReviewFilter => filtered.Where(device => device.RequiresPriorityReview),
            CriticalClassFilter => filtered.Where(device => device.IsCriticalClass),
            AllDevicesFilter => filtered,
            _ => filtered.Where(device => device.NeedsAttention)
        };

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filtered = filtered.Where(device =>
                device.FriendlyName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.DeviceClass.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.Manufacturer.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.ProviderLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.ServiceLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.InstanceId.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.InfLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.EvidenceTierLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.EvidenceTierDescription.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.HardwareIdsPreview.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.CompatibleIdsPreview.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.MatchConfidenceLabel.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || device.MatchConfidenceReason.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private IReadOnlyList<DriverDeviceRecord> ApplyFilters(IEnumerable<DriverDeviceRecord> devices)
    {
        return ApplyFilterCriteria(devices)
            .Take(64)
            .ToArray();
    }

    private void EnsureSelectedDevice()
    {
        if (Inventory is null)
        {
            SelectedDevice = null;
            return;
        }

        if (SelectedDevice is not null)
        {
            SelectedDevice = Inventory.Devices.FirstOrDefault(device => device.InstanceId == SelectedDevice.InstanceId);
        }

        if (SelectedDevice is not null && FilteredDevices.Any(device => device.InstanceId == SelectedDevice.InstanceId))
        {
            return;
        }

        SelectedDevice = FilteredDevices.FirstOrDefault()
            ?? Inventory.Devices.FirstOrDefault(device => device.RequiresPriorityReview)
            ?? Inventory.Devices.FirstOrDefault();
    }

    private void UpdateSelectedRemediationPlan()
    {
        SelectedRemediationPlan = SelectedDevice is null
            ? null
            : DriverRemediationPlanner.Build(SelectedDevice);
    }

    private void SyncFirmwareLookupState()
    {
        if (Firmware is null || LastFirmwareLookupResult is null)
        {
            LastFirmwareLookupResult = Firmware is null ? null : LastFirmwareLookupResult;
            return;
        }

        bool identityMatches = string.Equals(LastFirmwareLookupResult.ModelLabel, Firmware.SupportIdentityLabel, StringComparison.OrdinalIgnoreCase);
        bool currentVersionMatches = string.Equals(LastFirmwareLookupResult.CurrentVersion, Firmware.BiosVersionLabel, StringComparison.OrdinalIgnoreCase);

        if (!identityMatches || !currentVersionMatches)
        {
            LastFirmwareLookupResult = null;
        }
    }

    private void UpdateSelectedRepositoryCandidate()
    {
        if (SelectedDevice is null)
        {
            SelectedRepositoryCandidate = null;
            return;
        }

        IReadOnlyList<DriverRepositoryCandidate> candidates = RepositoryCandidates;
        if (SelectedRepositoryCandidate is not null)
        {
            SelectedRepositoryCandidate = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.InfPath, SelectedRepositoryCandidate.InfPath, StringComparison.OrdinalIgnoreCase));
        }

        SelectedRepositoryCandidate ??= candidates.FirstOrDefault();
    }

    private void SyncQueueSelection()
    {
        ApplyQueueSource();
        ApplyRepositoryCandidateSource();
        RefreshVisualState();
    }

    private async void RefreshDrivers_Click(object sender, RoutedEventArgs e)
    {
        _actionStatusMessage = "Refreshing the driver audit surface.";
        Bindings.Update();
        await RefreshAsync();
        _actionStatusMessage = "Driver and firmware audit refreshed from the current Windows inventory.";
        Bindings.Update();
    }

    private async void CheckLatestBios_Click(object sender, RoutedEventArgs e)
    {
        if (Firmware is null)
        {
            _actionStatusMessage = "Firmware inventory is not available yet.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        try
        {
            _actionStatusMessage = "Checking the official firmware route for the latest BIOS posture.";
            Bindings.Update();
            LastFirmwareLookupResult = await App.GetService<IFirmwareReleaseLookupService>()
                .LookupAsync(Firmware);
            _actionStatusMessage = LastFirmwareLookupResult.StatusLine;
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The latest BIOS lookup could not be completed.";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Latest firmware lookup failed.");
        }

        await RefreshFirmwareSafetyAssessmentAsync();
        Bindings.Update();
        RefreshVisualState();
    }

    private void CopyFlashPreparation_Click(object sender, RoutedEventArgs e)
    {
        if (!HasFirmwareFlashPreparation)
        {
            _actionStatusMessage = "Firmware flash preparation guidance is not available yet.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(CurrentFirmwareFlashPreparationGuide.ClipboardText, "Copied the firmware flash preparation guide to the clipboard.");
    }

    private void FilterModeBox_Loaded(object sender, RoutedEventArgs e)
    {
        FilterModeBox.SelectedIndex = 0;
    }

    private void FilterModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterModeBox.SelectedItem is not ComboBoxItem item || item.Content is not string filter)
        {
            return;
        }

        _activeFilter = filter;
        EnsureSelectedDevice();
        UpdateSelectedRemediationPlan();
        UpdateSelectedRepositoryCandidate();
        Bindings.Update();
        SyncQueueSelection();
    }

    private void DeviceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = DeviceSearchBox.Text?.Trim() ?? string.Empty;
        EnsureSelectedDevice();
        UpdateSelectedRemediationPlan();
        UpdateSelectedRepositoryCandidate();
        Bindings.Update();
        SyncQueueSelection();
    }

    private void DriverQueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingQueueSelection)
        {
            return;
        }

        if (DriverQueueList.SelectedItem is DriverDeviceRecord device)
        {
            SelectedDevice = device;
            UpdateSelectedRemediationPlan();
            UpdateSelectedRepositoryCandidate();
            Bindings.Update();
            ApplyRepositoryCandidateSource();
            RefreshVisualState();
        }
    }

    private void DriverQueueList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DriverDeviceRecord device)
        {
            return;
        }

        SelectedDevice = device;
        UpdateSelectedRemediationPlan();
        UpdateSelectedRepositoryCandidate();
        _actionStatusMessage = $"Selected {device.FriendlyName}. Review the selected-device panel and the recommended fix path below.";
        Bindings.Update();
        ApplyQueueSource();
        ApplyRepositoryCandidateSource();
        RefreshVisualState();
    }

    private async void ExportDriverAudit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Inventory is null)
            {
                await RefreshAsync();
            }

            if (Inventory is null)
            {
                _actionStatusMessage = "The driver audit inventory is not available for export.";
                Bindings.Update();
                return;
            }

            _actionStatusMessage = "Exporting the current driver audit.";
            Bindings.Update();
            LastExport = await App.GetService<IDriverAuditExportService>().ExportAsync(Inventory);
            _actionStatusMessage = "Driver audit exported to JSON and Markdown.";
            OpenExportFolderIfEnabled(LastExport.ExportDirectory, "Opened the driver audit export folder automatically.");
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The driver audit could not be exported.";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Driver audit export failed.");
        }

        Bindings.Update();
        RefreshVisualState();
    }

    private void RepositoryCandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingRepositorySelection)
        {
            return;
        }

        if (RepositoryCandidateList.SelectedItem is DriverRepositoryCandidate candidate)
        {
            SelectedRepositoryCandidate = candidate;
            Bindings.Update();
            RefreshVisualState();
        }
    }

    private async void InstallRepositoryCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null || SelectedRepositoryCandidate is null)
        {
            _actionStatusMessage = "Select a device and a local INF candidate first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        try
        {
            DriverDeviceRecord beforeInstall = SelectedDevice;
            DriverRepositoryCandidate selectedCandidate = SelectedRepositoryCandidate;
            bool dryRunEnabled = _settings?.DryRunEnabled ?? true;
            LastInstallVerificationResult = null;
            _actionStatusMessage = dryRunEnabled
                ? "Previewing the pnputil install command for the selected local candidate."
                : "Launching pnputil for the selected local candidate.";
            Bindings.Update();

            LastInstallResult = await App.GetService<IDriverInstallService>()
                .InstallAsync(beforeInstall, selectedCandidate, dryRunEnabled);

            _actionStatusMessage = LastInstallResult.Succeeded && !LastInstallResult.WasDryRun
                ? $"{LastInstallResult.StatusLine} Review Safety & Undo for the recorded install history."
                : LastInstallResult.StatusLine;

            if (!LastInstallResult.WasDryRun && LastInstallResult.Succeeded)
            {
                string selectedInstanceId = beforeInstall.InstanceId;
                await RefreshAsync();
                DriverDeviceRecord? afterInstall = Inventory?.Devices.FirstOrDefault(device => device.InstanceId == selectedInstanceId);
                LastInstallVerificationResult = App.GetService<IDriverInstallVerificationService>()
                    .Verify(beforeInstall, afterInstall, selectedCandidate);
                SelectedDevice = afterInstall ?? SelectedDevice;
                _actionStatusMessage = $"{LastInstallVerificationResult.Summary} Review Safety & Undo for the recorded install history.";
                UpdateSelectedRemediationPlan();
                UpdateSelectedRepositoryCandidate();
                await RefreshDriverStoreEvidenceAsync(SelectedDevice ?? beforeInstall, "Collecting driver-store evidence after the install attempt.");
                ApplyQueueSource();
                ApplyRepositoryCandidateSource();
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The local driver install could not be completed.";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Local driver install failed.");
        }

        Bindings.Update();
        RefreshVisualState();
    }

    private async Task RefreshDriverStoreEvidenceAsync(DriverDeviceRecord device, string progressStatus)
    {
        try
        {
            _actionStatusMessage = progressStatus;
            Bindings.Update();
            LastDriverStoreEvidenceResult = await App.GetService<IDriverStoreEvidenceService>()
                .CollectAsync(device);
            _actionStatusMessage = LastDriverStoreEvidenceResult.StatusLine;
        }
        catch (Exception ex)
        {
            LastDriverStoreEvidenceResult = null;
            _actionStatusMessage = "The driver-store evidence query could not be completed.";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Driver-store evidence query failed.");
        }
    }

    private async void SeedInstalledPackage_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        string? targetRoot = DepotScan?.ActiveRoots.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            _actionStatusMessage = "No active local driver repository root is available. Configure one in Settings first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        try
        {
            bool dryRunEnabled = _settings?.DryRunEnabled ?? true;
            _actionStatusMessage = dryRunEnabled
                ? "Previewing the pnputil export command for the installed package."
                : "Exporting the installed package into the local repository.";
            Bindings.Update();

            LastRepositorySeedResult = await App.GetService<IDriverRepositorySeedService>()
                .ExportInstalledPackageAsync(SelectedDevice, targetRoot, dryRunEnabled);

            _actionStatusMessage = LastRepositorySeedResult.StatusLine;

            if (!LastRepositorySeedResult.WasDryRun && LastRepositorySeedResult.Succeeded)
            {
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The installed driver package could not be exported into the local repository.";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Driver repository seed export failed.");
        }

        Bindings.Update();
        RefreshVisualState();
    }

    private async void RefreshDriverStoreEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        await RefreshDriverStoreEvidenceAsync(SelectedDevice, "Collecting driver-store evidence for the selected device.");
        Bindings.Update();
        RefreshVisualState();
    }

    private void CopyDriverStoreEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (!HasActiveDriverStoreEvidence)
        {
            _actionStatusMessage = "No driver-store evidence is available for the selected device yet.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(
            ActiveDriverStoreEvidenceResult!.RawOutput,
            "Copied the driver-store evidence to the clipboard.");
    }

    private void OpenSeedExportFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LastRepositorySeedResult is null || !Directory.Exists(LastRepositorySeedResult.ExportDirectory))
        {
            _actionStatusMessage = "The repository seed export folder is not available yet.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        LaunchExternal(
            "explorer.exe",
            LastRepositorySeedResult.ExportDirectory,
            "Opened the repository seed export folder.");
    }

    private void OpenRepositoryCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRepositoryCandidate is null || !File.Exists(SelectedRepositoryCandidate.InfPath))
        {
            _actionStatusMessage = "The selected local INF candidate is not available anymore.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        LaunchExternal(
            "explorer.exe",
            $"/select,\"{SelectedRepositoryCandidate.InfPath}\"",
            "Opened the selected local INF candidate.");
    }

    private void CopyRepositoryCandidatePath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRepositoryCandidate is null)
        {
            _actionStatusMessage = "Select a local INF candidate first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(
            SelectedRepositoryCandidate.InfPath,
            "Copied the selected local INF path to the clipboard.");
    }

    private void OpenRepositoryRoot_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRepositoryCandidate is null || !Directory.Exists(SelectedRepositoryCandidate.RepositoryRoot))
        {
            _actionStatusMessage = "The selected repository root is not currently available.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        LaunchExternal(
            "explorer.exe",
            SelectedRepositoryCandidate.RepositoryRoot,
            "Opened the selected repository root.");
    }

    private void CopyHardwareIds_Click(object sender, RoutedEventArgs e)
    {
        if (!HasSelectedHardwareIds)
        {
            _actionStatusMessage = "The selected device does not currently expose hardware IDs.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(SelectedDevice!.HardwareIdsPreview, "Copied the selected device hardware IDs to the clipboard.");
    }

    private void CopyCompatibleIds_Click(object sender, RoutedEventArgs e)
    {
        if (!HasSelectedCompatibleIds)
        {
            _actionStatusMessage = "The selected device does not currently expose compatible IDs.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(SelectedDevice!.CompatibleIdsPreview, "Copied the selected device compatible IDs to the clipboard.");
    }

    private void CopyTechnicianBrief_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(SelectedDeviceTechnicianHandoff, "Copied the technician handoff summary to the clipboard.");
    }

    private void CopyRemediationPlan_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null || SelectedRemediationPlan is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        CopyTextToClipboard(
            DriverRemediationDocumentFormatter.BuildClipboardText(SelectedDevice, SelectedRemediationPlan),
            "Copied the selected remediation plan to the clipboard.");
    }

    private void CopyVerificationChecklist_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null || SelectedRemediationPlan is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        string checklist = string.Join(
            Environment.NewLine,
            SelectedPlanVerificationSteps.Select((step, index) => $"{index + 1}. {step.Title}: {step.Detail}"));
        CopyTextToClipboard(checklist, "Copied the verification checklist to the clipboard.");
    }

    private void OpenAuditFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LastExport is null || !Directory.Exists(LastExport.ExportDirectory))
        {
            _actionStatusMessage = "The driver audit export folder is not available yet.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", LastExport.ExportDirectory, "Opened the driver audit export folder.");
    }

    private async void ExportRemediationPlan_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null || SelectedRemediationPlan is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        try
        {
            _actionStatusMessage = "Exporting the selected remediation plan.";
            Bindings.Update();
            LastPlanExport = await App.GetService<IDriverRemediationExportService>()
                .ExportAsync(SelectedDevice, SelectedRemediationPlan);
            _actionStatusMessage = "Selected remediation plan exported to JSON and Markdown.";
            OpenExportFolderIfEnabled(LastPlanExport.ExportDirectory, "Opened the remediation plan export folder automatically.");
        }
        catch (Exception ex)
        {
            _actionStatusMessage = "The selected remediation plan could not be exported.";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Driver remediation export failed.");
        }

        Bindings.Update();
        RefreshVisualState();
    }

    private void OpenPlanFolder_Click(object sender, RoutedEventArgs e)
    {
        if (LastPlanExport is null || !Directory.Exists(LastPlanExport.ExportDirectory))
        {
            _actionStatusMessage = "The remediation plan export folder is not available yet.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", LastPlanExport.ExportDirectory, "Opened the remediation plan export folder.");
    }

    private void OpenExportFolderIfEnabled(string? exportDirectory, string successMessage)
    {
        if (_settings?.OpenExportFolderAfterExport != true
            || string.IsNullOrWhiteSpace(exportDirectory)
            || !Directory.Exists(exportDirectory))
        {
            return;
        }

        LaunchExternal("explorer.exe", exportDirectory, successMessage);
    }

    private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
    {
        LaunchExternal("devmgmt.msc", null, "Opened Device Manager.");
    }

    private void OpenWindowsUpdate_Click(object sender, RoutedEventArgs e)
    {
        LaunchExternal("ms-settings:windowsupdate", null, "Opened Windows Update settings.");
    }

    private void OpenFirmwareSupport_Click(object sender, RoutedEventArgs e)
    {
        string? targetUrl = LastFirmwareLookupResult?.HasDetailsUrl == true
            ? LastFirmwareLookupResult.DetailsUrl
            : LastFirmwareLookupResult?.HasSupportUrl == true
                ? LastFirmwareLookupResult.SupportUrl
                : Firmware?.PrimarySupportUrl;

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            _actionStatusMessage = "No official firmware support route is mapped for this machine yet.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        LaunchExternal(targetUrl, null, "Opened the official firmware support route.");
    }

    private void OpenSystemInformation_Click(object sender, RoutedEventArgs e)
    {
        LaunchExternal("msinfo32.exe", null, "Opened System Information.");
    }

    private void CopyFirmwareBrief_Click(object sender, RoutedEventArgs e)
    {
        if (!HasFirmwareSnapshot && !HasFirmwareLookupResult && !HasFirmwareSafetyAssessment)
        {
            _actionStatusMessage = "Firmware inventory is not available yet.";
            Bindings.Update();
            RefreshVisualState();
            return;
        }

        List<string> sections = [];

        if (HasFirmwareSnapshot)
        {
            sections.Add(FirmwareBrief);
        }

        if (HasFirmwareLookupResult)
        {
            sections.Add(LastFirmwareLookupResult!.ClipboardText);
        }

        if (HasFirmwareSafetyAssessment)
        {
            sections.Add(FirmwareSafetyAssessment!.ClipboardText);
        }

        if (HasFirmwareFlashPreparation)
        {
            sections.Add(CurrentFirmwareFlashPreparationGuide.ClipboardText);
        }

        string text = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            sections.Where(section => !string.IsNullOrWhiteSpace(section)));

        CopyTextToClipboard(text, "Copied the firmware brief to the clipboard.");
    }

    private FirmwareFlashPreparationGuide CurrentFirmwareFlashPreparationGuide =>
        FirmwareFlashPreparationAdvisor.Build(Firmware, LastFirmwareLookupResult, FirmwareSafetyAssessment);

    private async Task RefreshFirmwareSafetyAssessmentAsync()
    {
        if (Firmware is null)
        {
            FirmwareSafetyAssessment = null;
            return;
        }

        try
        {
            FirmwareSafetyAssessment = await App.GetService<IFirmwareSafetyAssessmentService>()
                .AssessAsync(Firmware, LastFirmwareLookupResult);
        }
        catch (Exception ex)
        {
            FirmwareSafetyAssessment = null;
            App.GetService<ILogger<DriversPage>>().LogWarning(ex, "Firmware safety assessment failed.");
        }
    }

    private void RunSelectedPrimaryDriverAction_Click(object sender, RoutedEventArgs e) =>
        ExecuteSelectedDriverAction(CurrentSelectedDriverNextAction.PrimaryActionKind);

    private void RunSelectedSecondaryDriverAction_Click(object sender, RoutedEventArgs e) =>
        ExecuteSelectedDriverAction(CurrentSelectedDriverNextAction.SecondaryActionKind);

    private void ExecuteSelectedDriverAction(DriverReviewNextActionKind actionKind)
    {
        switch (actionKind)
        {
            case DriverReviewNextActionKind.InstallSelectedLocalDriver:
                InstallRepositoryCandidate_Click(this, new RoutedEventArgs());
                return;
            case DriverReviewNextActionKind.OpenDeviceManager:
                OpenDeviceManager_Click(this, new RoutedEventArgs());
                return;
            case DriverReviewNextActionKind.OpenWindowsUpdate:
                OpenWindowsUpdate_Click(this, new RoutedEventArgs());
                return;
            case DriverReviewNextActionKind.CopyTechnicianBrief:
                CopyTechnicianBrief_Click(this, new RoutedEventArgs());
                return;
            case DriverReviewNextActionKind.OpenSettings:
                App.GetService<MainWindow>().NavigateToSection(AppSection.Settings);
                _actionStatusMessage = "Opened Settings so you can change local driver repository roots.";
                Bindings.Update();
                RefreshVisualState();
                return;
            case DriverReviewNextActionKind.OpenSelectedInf:
                OpenSelectedInf_Click(this, new RoutedEventArgs());
                return;
            default:
                _actionStatusMessage = "Select a device first.";
                Bindings.Update();
                RefreshVisualState();
                return;
        }
    }

    private void OpenSelectedInf_Click(object sender, RoutedEventArgs e)
    {
        string? infPath = GetSelectedInfPath();
        if (string.IsNullOrWhiteSpace(infPath) || !File.Exists(infPath))
        {
            _actionStatusMessage = "The selected device does not expose a local INF path that can be opened.";
            Bindings.Update();
            return;
        }

        LaunchExternal("explorer.exe", $"/select,\"{infPath}\"", "Opened the INF location for the selected device.");
    }

    private void CopyInstanceId_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDevice is null)
        {
            _actionStatusMessage = "Select a device first.";
            Bindings.Update();
            return;
        }

        CopyTextToClipboard(SelectedDevice.InstanceId, "Copied the device instance ID to the clipboard.");
    }

    private void CopyTextToClipboard(string text, string successMessage)
    {
        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
        _actionStatusMessage = successMessage;
        Bindings.Update();
        RefreshVisualState();
    }

    private string? GetSelectedInfPath()
    {
        if (SelectedDevice is null || string.IsNullOrWhiteSpace(SelectedDevice.InfName))
        {
            return null;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "INF",
            SelectedDevice.InfName);
    }

    private void DriversPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyAdaptiveLayout(e.NewSize.Width);
    }

    private void ApplyAdaptiveLayout(double width)
    {
        if (width >= WideLayoutBreakpoint)
        {
            ApplyWideLayout();
            return;
        }

        if (width >= MediumLayoutBreakpoint)
        {
            ApplyMediumLayout();
            return;
        }

        ApplyNarrowLayout();
    }

    private void ApplyWideLayout()
    {
        LayoutRoot.Padding = new Thickness(36, 28, 36, 40);

        HeroAuditColumn.Width = new GridLength(280);
        HeroAuditRow.Height = new GridLength(0);
        Grid.SetRow(HeroAuditCard, 0);
        Grid.SetColumn(HeroAuditCard, 1);

        MetricColumn2.Width = new GridLength(1, GridUnitType.Star);
        MetricColumn3.Width = new GridLength(1, GridUnitType.Star);
        MetricColumn4.Width = new GridLength(1, GridUnitType.Star);
        MetricRow2.Height = new GridLength(0);
        MetricRow3.Height = new GridLength(0);
        MetricRow4.Height = new GridLength(0);
        Grid.SetRow(MetricPriorityCard, 0);
        Grid.SetColumn(MetricPriorityCard, 0);
        Grid.SetRow(MetricOemCard, 0);
        Grid.SetColumn(MetricOemCard, 1);
        Grid.SetRow(MetricFallbackCard, 0);
        Grid.SetColumn(MetricFallbackCard, 2);
        Grid.SetRow(MetricNoIdentifierCard, 0);
        Grid.SetColumn(MetricNoIdentifierCard, 3);

        QueueColumn.Width = new GridLength(580);
        DetailsColumn.Width = new GridLength(1, GridUnitType.Star);
        QueueRow.Height = GridLength.Auto;
        DetailsRow.Height = new GridLength(0);
        Grid.SetRow(QueuePanel, 0);
        Grid.SetColumn(QueuePanel, 0);
        Grid.SetRow(DetailsPanel, 0);
        Grid.SetColumn(DetailsPanel, 1);

        if (DriverQueueList is not null)
        {
            DriverQueueList.Height = 760;
        }

        QueueFilterColumn.Width = new GridLength(220);
        QueueFilterRow.Height = new GridLength(0);
        Grid.SetRow(FilterModeBox, 0);
        Grid.SetColumn(FilterModeBox, 1);
    }

    private void ApplyMediumLayout()
    {
        LayoutRoot.Padding = new Thickness(28, 20, 28, 28);

        HeroAuditColumn.Width = new GridLength(0);
        HeroAuditRow.Height = GridLength.Auto;
        Grid.SetRow(HeroAuditCard, 1);
        Grid.SetColumn(HeroAuditCard, 0);

        MetricColumn2.Width = new GridLength(1, GridUnitType.Star);
        MetricColumn3.Width = new GridLength(0);
        MetricColumn4.Width = new GridLength(0);
        MetricRow2.Height = GridLength.Auto;
        MetricRow3.Height = new GridLength(0);
        MetricRow4.Height = new GridLength(0);
        Grid.SetRow(MetricPriorityCard, 0);
        Grid.SetColumn(MetricPriorityCard, 0);
        Grid.SetRow(MetricOemCard, 0);
        Grid.SetColumn(MetricOemCard, 1);
        Grid.SetRow(MetricFallbackCard, 1);
        Grid.SetColumn(MetricFallbackCard, 0);
        Grid.SetRow(MetricNoIdentifierCard, 1);
        Grid.SetColumn(MetricNoIdentifierCard, 1);

        QueueColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailsColumn.Width = new GridLength(0);
        QueueRow.Height = GridLength.Auto;
        DetailsRow.Height = GridLength.Auto;
        Grid.SetRow(QueuePanel, 0);
        Grid.SetColumn(QueuePanel, 0);
        Grid.SetRow(DetailsPanel, 1);
        Grid.SetColumn(DetailsPanel, 0);

        if (DriverQueueList is not null)
        {
            DriverQueueList.Height = 680;
        }

        QueueFilterColumn.Width = new GridLength(220);
        QueueFilterRow.Height = new GridLength(0);
        Grid.SetRow(FilterModeBox, 0);
        Grid.SetColumn(FilterModeBox, 1);
    }

    private void ApplyNarrowLayout()
    {
        LayoutRoot.Padding = new Thickness(20, 16, 20, 24);

        HeroAuditColumn.Width = new GridLength(0);
        HeroAuditRow.Height = GridLength.Auto;
        Grid.SetRow(HeroAuditCard, 1);
        Grid.SetColumn(HeroAuditCard, 0);

        MetricColumn2.Width = new GridLength(0);
        MetricColumn3.Width = new GridLength(0);
        MetricColumn4.Width = new GridLength(0);
        MetricRow2.Height = GridLength.Auto;
        MetricRow3.Height = GridLength.Auto;
        MetricRow4.Height = GridLength.Auto;
        Grid.SetRow(MetricPriorityCard, 0);
        Grid.SetColumn(MetricPriorityCard, 0);
        Grid.SetRow(MetricOemCard, 1);
        Grid.SetColumn(MetricOemCard, 0);
        Grid.SetRow(MetricFallbackCard, 2);
        Grid.SetColumn(MetricFallbackCard, 0);
        Grid.SetRow(MetricNoIdentifierCard, 3);
        Grid.SetColumn(MetricNoIdentifierCard, 0);

        QueueColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailsColumn.Width = new GridLength(0);
        QueueRow.Height = GridLength.Auto;
        DetailsRow.Height = GridLength.Auto;
        Grid.SetRow(QueuePanel, 0);
        Grid.SetColumn(QueuePanel, 0);
        Grid.SetRow(DetailsPanel, 1);
        Grid.SetColumn(DetailsPanel, 0);

        if (DriverQueueList is not null)
        {
            DriverQueueList.Height = 560;
        }

        QueueFilterColumn.Width = new GridLength(0);
        QueueFilterRow.Height = GridLength.Auto;
        Grid.SetRow(FilterModeBox, 1);
        Grid.SetColumn(FilterModeBox, 0);
    }

    private void LaunchExternal(string fileName, string? arguments, string successMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            });

            _actionStatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            _actionStatusMessage = $"The requested Windows surface could not be opened: {ex.Message}";
            App.GetService<ILogger<DriversPage>>().LogError(ex, "Driver review action failed for {FileName}.", fileName);
        }

        Bindings.Update();
        RefreshVisualState();
    }
}
