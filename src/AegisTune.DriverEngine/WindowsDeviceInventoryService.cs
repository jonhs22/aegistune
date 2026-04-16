using System.Management;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.DriverEngine;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceInventoryService : IDeviceInventoryService
{
    public Task<DeviceInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            DateTimeOffset scannedAt = DateTimeOffset.Now;

            try
            {
                IReadOnlyDictionary<string, DriverMetadata> driverMetadata = LoadDriverMetadata();
                DriverDeviceRecord[] devices = LoadDevices(driverMetadata);
                string? warningMessage = devices.Length == 0 ? "WMI returned no Plug and Play devices for this scan." : null;

                return new DeviceInventorySnapshot(devices, scannedAt, warningMessage);
            }
            catch (Exception ex)
            {
                return new DeviceInventorySnapshot(Array.Empty<DriverDeviceRecord>(), scannedAt, $"Device inventory failed: {ex.Message}");
            }
        }, cancellationToken);

    private static IReadOnlyDictionary<string, DriverMetadata> LoadDriverMetadata()
    {
        var metadataByDeviceId = new Dictionary<string, DriverMetadata>(StringComparer.OrdinalIgnoreCase);

        using ManagementObjectSearcher searcher = new(
            "SELECT DeviceID, DriverProviderName, DriverVersion, InfName, DriverDate, IsSigned, Signer FROM Win32_PnPSignedDriver");

        foreach (ManagementObject driver in searcher.Get())
        {
            string? deviceId = GetString(driver, "DeviceID");
            if (string.IsNullOrWhiteSpace(deviceId) || metadataByDeviceId.ContainsKey(deviceId))
            {
                continue;
            }

            metadataByDeviceId[deviceId] = new DriverMetadata(
                GetString(driver, "DriverProviderName") ?? string.Empty,
                GetString(driver, "DriverVersion") ?? string.Empty,
                GetString(driver, "InfName"),
                GetDateTimeOffset(driver, "DriverDate"),
                GetBoolean(driver, "IsSigned"),
                GetString(driver, "Signer"));
        }

        return metadataByDeviceId;
    }

    private static DriverDeviceRecord[] LoadDevices(IReadOnlyDictionary<string, DriverMetadata> driverMetadata)
    {
        var devices = new List<DriverDeviceRecord>();

        using ManagementObjectSearcher searcher = new(
            "SELECT DeviceID, Name, Manufacturer, PNPClass, Status, ConfigManagerErrorCode, HardwareID, CompatibleID, ClassGuid, Service, Present FROM Win32_PnPEntity");

        foreach (ManagementObject entity in searcher.Get())
        {
            string? deviceId = GetString(entity, "DeviceID");
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            driverMetadata.TryGetValue(deviceId, out DriverMetadata? metadata);

            devices.Add(new DriverDeviceRecord(
                GetString(entity, "Name") ?? deviceId,
                GetString(entity, "PNPClass") ?? "Unknown class",
                GetString(entity, "Manufacturer") ?? "Unknown manufacturer",
                metadata?.DriverProvider ?? string.Empty,
                metadata?.DriverVersion ?? string.Empty,
                GetString(entity, "Status") ?? "Unknown",
                GetInt32(entity, "ConfigManagerErrorCode"),
                deviceId,
                metadata?.InfName,
                metadata?.DriverDate,
                metadata?.IsSigned,
                metadata?.SignerName,
                GetString(entity, "ClassGuid"),
                GetString(entity, "Service"),
                GetBoolean(entity, "Present"),
                GetStringArray(entity, "HardwareID"),
                GetStringArray(entity, "CompatibleID")));
        }

        return devices
            .OrderByDescending(device => device.ReviewRiskLevel)
            .ThenByDescending(device => device.ProblemCode != 0)
            .ThenByDescending(device => device.HasSigningConcern)
            .ThenByDescending(device => device.EvidenceTier)
            .ThenByDescending(device => device.MatchConfidence)
            .ThenByDescending(device => device.UsesGenericProviderReview)
            .ThenByDescending(device => device.IsCriticalClass)
            .ThenBy(device => device.DeviceClass, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetString(ManagementBaseObject instance, string propertyName) =>
        instance[propertyName]?.ToString();

    private static int GetInt32(ManagementBaseObject instance, string propertyName)
    {
        object? value = instance[propertyName];
        return value switch
        {
            null => 0,
            int intValue => intValue,
            uint uintValue => unchecked((int)uintValue),
            ushort ushortValue => ushortValue,
            _ when int.TryParse(value.ToString(), out int parsedValue) => parsedValue,
            _ => 0
        };
    }

    private static bool? GetBoolean(ManagementBaseObject instance, string propertyName)
    {
        object? value = instance[propertyName];
        return value switch
        {
            null => null,
            bool booleanValue => booleanValue,
            string stringValue when bool.TryParse(stringValue, out bool parsedBoolean) => parsedBoolean,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            _ => null
        };
    }

    private static IReadOnlyList<string> GetStringArray(ManagementBaseObject instance, string propertyName)
    {
        object? value = instance[propertyName];
        return value switch
        {
            string[] items => items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Array items => items.Cast<object?>()
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => Array.Empty<string>()
        };
    }

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

    private sealed record DriverMetadata(
        string DriverProvider,
        string DriverVersion,
        string? InfName,
        DateTimeOffset? DriverDate,
        bool? IsSigned,
        string? SignerName);
}
