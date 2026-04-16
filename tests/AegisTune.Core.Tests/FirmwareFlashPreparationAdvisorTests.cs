using AegisTune.Core;

namespace AegisTune.Core.Tests;

public sealed class FirmwareFlashPreparationAdvisorTests
{
    [Fact]
    public void Build_IncludesReleaseNotesPreviewAndBitLockerCommandsWhenTargetIsDeterministic()
    {
        FirmwareFlashPreparationGuide guide = FirmwareFlashPreparationAdvisor.Build(
            CreateFirmwareSnapshot(),
            new FirmwareReleaseLookupResult(
                FirmwareReleaseLookupMode.CatalogFeed,
                "HP",
                "HP EliteBook 840 G8 Notebook PC",
                "01.20.00",
                new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
                "Latest BIOS verified.",
                "Use HP Support.",
                "Official HP Support lists BIOS 01.24.01 Rev.A.",
                "Search HP support.",
                "https://support.hp.com/us-en/drivers/hp-elitebook-840-g8-notebook-pc/38216725",
                "https://support.hp.com/soar-attachment/144/col110274-ob-361835-1_sp172011_releasedoc.html",
                "01.24.01 Rev.A",
                new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero),
                "HP Image Assistant / HP CMSL",
                "Use HP tooling.",
                "Official HP Support",
                null,
                new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero),
                false,
                "HP BIOS and System Firmware (T37/T39/T76)",
                "This package creates files that contain an image of the System BIOS (ROM) for the supported computer models."),
            new FirmwareSafetyAssessment(
                "HP EliteBook 840 G8 Notebook PC",
                "C:",
                "BitLocker protection is active on C:.",
                "AC power is connected.",
                new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero),
                Array.Empty<FirmwareSafetyGate>(),
                null,
                true,
                true,
                true,
                94));

        Assert.Contains("staged target", guide.TargetSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("official release details", guide.ReleaseNotesSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("System BIOS", guide.ReleaseNotesPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Suspend-BitLocker", guide.CommandPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Review the official release notes", guide.ChecklistPreview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RequiresManualTargetWhenNoDeterministicLookupExists()
    {
        FirmwareFlashPreparationGuide guide = FirmwareFlashPreparationAdvisor.Build(
            CreateFirmwareSnapshot(),
            null,
            new FirmwareSafetyAssessment(
                "ASUS TUF B450-PLUS GAMING",
                "C:",
                "BitLocker status unavailable.",
                "Power status is inconclusive.",
                new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero),
                Array.Empty<FirmwareSafetyGate>()));

        Assert.Contains("no deterministic target is cached", guide.TargetSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manual vendor-review path", guide.ReleaseNotesSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-BitLockerVolume", guide.CommandPreview, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Suspend-BitLocker", guide.CommandPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Record the exact BIOS package manually", guide.ChecklistPreview, StringComparison.OrdinalIgnoreCase);
    }

    private static FirmwareInventorySnapshot CreateFirmwareSnapshot() =>
        new(
            "HP",
            "EliteBook 840 G8",
            "HP",
            "HP EliteBook 840 G8 Notebook PC",
            "HP",
            "01.20.00",
            "HP 01.20.00",
            new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            "UEFI",
            true,
            "HP",
            "HP EliteBook 840 G8 Notebook PC",
            "System model identity",
            "Official HP firmware route",
            "https://support.hp.com/us-en/drivers",
            "Search the official HP support page before any firmware change.",
            "Firmware review is aligned to HP EliteBook 840 G8 Notebook PC.",
            Array.Empty<FirmwareSupportOption>(),
            new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero));
}
