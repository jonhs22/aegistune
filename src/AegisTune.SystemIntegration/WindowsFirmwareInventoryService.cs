using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AegisTune.Core;
using Microsoft.Win32;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsFirmwareInventoryService : IFirmwareInventoryService
{
    public Task<FirmwareInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            DateTimeOffset collectedAt = DateTimeOffset.Now;
            var warnings = new List<string>();

            BiosRecord bios = LoadBios(warnings);
            SystemRecord system = LoadSystem(warnings);
            ProductRecord product = LoadComputerSystemProduct(warnings);
            BoardRecord board = LoadBoard(warnings);
            string firmwareMode = GetFirmwareMode(warnings);
            bool? secureBootEnabled = GetSecureBootEnabled();

            string systemManufacturer = FirstAvailable(system.Manufacturer, product.Vendor);
            string systemModel = FirstAvailable(system.Model, product.Name, product.Version);
            string? warningMessage = warnings.Count == 0
                ? null
                : string.Join(" ", warnings.Distinct(StringComparer.Ordinal));

            return FirmwareSupportAdvisor.Build(
                systemManufacturer,
                systemModel,
                board.Manufacturer,
                board.Product,
                bios.Manufacturer,
                bios.Version,
                bios.FamilyVersion,
                bios.ReleaseDate,
                firmwareMode,
                secureBootEnabled,
                collectedAt,
                warningMessage);
        }, cancellationToken);

    private static BiosRecord LoadBios(List<string> warnings)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(
                "SELECT Manufacturer, SMBIOSBIOSVersion, Version, ReleaseDate FROM Win32_BIOS");

            ManagementObject? bios = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (bios is null)
            {
                warnings.Add("Windows returned no BIOS data for this machine.");
                return new BiosRecord(null, null, null, null);
            }

            return new BiosRecord(
                GetString(bios, "Manufacturer"),
                FirstAvailable(GetString(bios, "SMBIOSBIOSVersion"), GetString(bios, "Version")),
                GetString(bios, "Version"),
                GetDateTimeOffset(bios, "ReleaseDate"));
        }
        catch (Exception ex)
        {
            warnings.Add($"BIOS inventory failed: {ex.Message}");
            return new BiosRecord(null, null, null, null);
        }
    }

    private static SystemRecord LoadSystem(List<string> warnings)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(
                "SELECT Manufacturer, Model FROM Win32_ComputerSystem");

            ManagementObject? system = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            return system is null
                ? new SystemRecord(null, null)
                : new SystemRecord(
                    GetString(system, "Manufacturer"),
                    GetString(system, "Model"));
        }
        catch (Exception ex)
        {
            warnings.Add($"System identity inventory failed: {ex.Message}");
            return new SystemRecord(null, null);
        }
    }

    private static ProductRecord LoadComputerSystemProduct(List<string> warnings)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(
                "SELECT Vendor, Name, Version FROM Win32_ComputerSystemProduct");

            ManagementObject? product = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            return product is null
                ? new ProductRecord(null, null, null)
                : new ProductRecord(
                    GetString(product, "Vendor"),
                    GetString(product, "Name"),
                    GetString(product, "Version"));
        }
        catch (Exception ex)
        {
            warnings.Add($"Computer product inventory failed: {ex.Message}");
            return new ProductRecord(null, null, null);
        }
    }

    private static BoardRecord LoadBoard(List<string> warnings)
    {
        try
        {
            using ManagementObjectSearcher searcher = new(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard");

            ManagementObject? board = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            return board is null
                ? new BoardRecord(null, null)
                : new BoardRecord(
                    GetString(board, "Manufacturer"),
                    GetString(board, "Product"));
        }
        catch (Exception ex)
        {
            warnings.Add($"Baseboard inventory failed: {ex.Message}");
            return new BoardRecord(null, null);
        }
    }

    private static string GetFirmwareMode(List<string> warnings)
    {
        try
        {
            return NativeMethods.GetFirmwareMode() switch
            {
                FirmwareType.Bios => "Legacy BIOS",
                FirmwareType.Uefi => "UEFI",
                _ => "Firmware mode unknown"
            };
        }
        catch (Exception ex)
        {
            warnings.Add($"Firmware mode check failed: {ex.Message}");
            return "Firmware mode unknown";
        }
    }

    private static bool? GetSecureBootEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            object? value = key?.GetValue("UEFISecureBootEnabled");
            return value switch
            {
                1 or 1U or 1L => true,
                0 or 0U or 0L => false,
                string text when int.TryParse(text, out int parsed) => parsed != 0,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(ManagementBaseObject instance, string propertyName) =>
        instance[propertyName]?.ToString();

    private static DateTimeOffset? GetDateTimeOffset(ManagementBaseObject instance, string propertyName)
    {
        string? value = GetString(instance, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            DateTime localDateTime = ManagementDateTimeConverter.ToDateTime(value);
            return new DateTimeOffset(localDateTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string FirstAvailable(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record BiosRecord(
        string? Manufacturer,
        string? Version,
        string? FamilyVersion,
        DateTimeOffset? ReleaseDate);

    private sealed record SystemRecord(
        string? Manufacturer,
        string? Model);

    private sealed record ProductRecord(
        string? Vendor,
        string? Name,
        string? Version);

    private sealed record BoardRecord(
        string? Manufacturer,
        string? Product);

    private enum FirmwareType : uint
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2,
        Max = 3
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFirmwareType(out FirmwareType firmwareType);

        public static FirmwareType GetFirmwareMode()
        {
            return GetFirmwareType(out FirmwareType firmwareType)
                ? firmwareType
                : FirmwareType.Unknown;
        }
    }
}
