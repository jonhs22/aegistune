using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AegisTune.Core;

namespace AegisTune.SystemIntegration;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioPlatformAdapter : IAudioPlatformAdapter
{
    private static readonly PropertyKey FriendlyNamePropertyKey = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    private static readonly Guid EmptyEventContext = Guid.Empty;

    public IReadOnlyList<AudioEndpointRecord> EnumerateActiveEndpoints()
    {
        IMMDeviceEnumerator enumerator = CreateEnumerator();

        try
        {
            string? defaultPlaybackId = TryGetDefaultEndpointId(enumerator, EDataFlow.Render, ERole.Multimedia);
            string? defaultPlaybackCommunicationId = TryGetDefaultEndpointId(enumerator, EDataFlow.Render, ERole.Communications);
            string? defaultRecordingId = TryGetDefaultEndpointId(enumerator, EDataFlow.Capture, ERole.Multimedia);
            string? defaultRecordingCommunicationId = TryGetDefaultEndpointId(enumerator, EDataFlow.Capture, ERole.Communications);

            List<AudioEndpointRecord> endpoints = [];
            endpoints.AddRange(EnumerateDevices(enumerator, EDataFlow.Render, AudioEndpointKind.Playback, defaultPlaybackId, defaultPlaybackCommunicationId));
            endpoints.AddRange(EnumerateDevices(enumerator, EDataFlow.Capture, AudioEndpointKind.Recording, defaultRecordingId, defaultRecordingCommunicationId));
            return endpoints;
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    public AudioEndpointRecord SetVolume(string deviceId, int targetPercent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        AudioEndpointRecord updatedEndpoint = ExecuteOnEndpoint(
            deviceId,
            endpointVolume =>
            {
                float scalar = Math.Clamp(targetPercent, 0, 100) / 100f;
                Guid eventContext = EmptyEventContext;
                ThrowIfFailed(endpointVolume.SetMasterVolumeLevelScalar(scalar, ref eventContext));
            });
        return updatedEndpoint;
    }

    public AudioEndpointRecord AdjustVolume(string deviceId, int deltaPercent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        AudioEndpointRecord currentEndpoint = GetEndpoint(deviceId);
        return SetVolume(deviceId, currentEndpoint.VolumePercent + deltaPercent);
    }

    public AudioEndpointRecord SetMute(string deviceId, bool isMuted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        AudioEndpointRecord updatedEndpoint = ExecuteOnEndpoint(
            deviceId,
            endpointVolume =>
            {
                Guid eventContext = EmptyEventContext;
                ThrowIfFailed(endpointVolume.SetMute(isMuted, ref eventContext));
            });
        return updatedEndpoint;
    }

    private AudioEndpointRecord ExecuteOnEndpoint(string deviceId, Action<IAudioEndpointVolume> applyChange)
    {
        IMMDeviceEnumerator enumerator = CreateEnumerator();
        IMMDevice? device = null;
        IAudioEndpointVolume? endpointVolume = null;

        try
        {
            ThrowIfFailed(enumerator.GetDevice(deviceId, out device));
            endpointVolume = ActivateEndpointVolume(device);
            applyChange(endpointVolume);
        }
        finally
        {
            ReleaseComObject(endpointVolume);
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }

        return GetEndpoint(deviceId);
    }

    private AudioEndpointRecord GetEndpoint(string deviceId) =>
        EnumerateActiveEndpoints()
            .FirstOrDefault(endpoint => string.Equals(endpoint.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Windows no longer exposes the requested audio endpoint: {deviceId}");

    private static IMMDeviceEnumerator CreateEnumerator() =>
        (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

    private static IEnumerable<AudioEndpointRecord> EnumerateDevices(
        IMMDeviceEnumerator enumerator,
        EDataFlow dataFlow,
        AudioEndpointKind kind,
        string? defaultEndpointId,
        string? defaultCommunicationEndpointId)
    {
        ThrowIfFailed(enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active, out IMMDeviceCollection? collection));

        try
        {
            ThrowIfFailed(collection.GetCount(out uint count));
            List<AudioEndpointRecord> endpoints = new((int)count);

            for (uint index = 0; index < count; index++)
            {
                ThrowIfFailed(collection.Item(index, out IMMDevice? device));
                try
                {
                    endpoints.Add(BuildEndpointRecord(device, kind, defaultEndpointId, defaultCommunicationEndpointId));
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }

            return endpoints;
        }
        finally
        {
            ReleaseComObject(collection);
        }
    }

    private static AudioEndpointRecord BuildEndpointRecord(
        IMMDevice device,
        AudioEndpointKind kind,
        string? defaultEndpointId,
        string? defaultCommunicationEndpointId)
    {
        ThrowIfFailed(device.GetId(out string? deviceId));
        ThrowIfFailed(device.GetState(out DeviceState state));

        string friendlyName = ReadFriendlyName(device) ?? deviceId ?? "Audio endpoint";
        IAudioEndpointVolume? endpointVolume = null;

        try
        {
            endpointVolume = ActivateEndpointVolume(device);
            ThrowIfFailed(endpointVolume.GetMasterVolumeLevelScalar(out float volumeScalar));
            ThrowIfFailed(endpointVolume.GetMute(out bool isMuted));

            return new AudioEndpointRecord(
                deviceId ?? string.Empty,
                friendlyName,
                kind,
                string.Equals(deviceId, defaultEndpointId, StringComparison.OrdinalIgnoreCase),
                string.Equals(deviceId, defaultCommunicationEndpointId, StringComparison.OrdinalIgnoreCase),
                (int)Math.Round(Math.Clamp(volumeScalar, 0f, 1f) * 100f),
                isMuted,
                state == DeviceState.Active ? "Active" : state.ToString());
        }
        finally
        {
            ReleaseComObject(endpointVolume);
        }
    }

    private static IAudioEndpointVolume ActivateEndpointVolume(IMMDevice device)
    {
        Guid interfaceId = typeof(IAudioEndpointVolume).GUID;
        ThrowIfFailed(device.Activate(ref interfaceId, ClsCtxInprocServer, IntPtr.Zero, out object? volumeObject));
        return (IAudioEndpointVolume)volumeObject;
    }

    private static string? ReadFriendlyName(IMMDevice device)
    {
        ThrowIfFailed(device.OpenPropertyStore(StorageAccess.Read, out IPropertyStore? propertyStore));

        try
        {
            PropertyKey propertyKey = FriendlyNamePropertyKey;
            ThrowIfFailed(propertyStore.GetValue(ref propertyKey, out PropVariant value));
            try
            {
                return value.GetValue();
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            ReleaseComObject(propertyStore);
        }
    }

    private static string? TryGetDefaultEndpointId(IMMDeviceEnumerator enumerator, EDataFlow dataFlow, ERole role)
    {
        int result = enumerator.GetDefaultAudioEndpoint(dataFlow, role, out IMMDevice? device);
        if (result != 0 || device is null)
        {
            return null;
        }

        try
        {
            ThrowIfFailed(device.GetId(out string? deviceId));
            return deviceId;
        }
        finally
        {
            ReleaseComObject(device);
        }
    }

    private static void ThrowIfFailed(int hResult)
    {
        if (hResult < 0)
        {
            Marshal.ThrowExceptionForHR(hResult);
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.ReleaseComObject(instance);
        }
    }

    private const int ClsCtxInprocServer = 0x1;

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string endpointId, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr callback);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-C0A72F7E2F55")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid interfaceId, int classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

        [PreserveSig]
        int OpenPropertyStore(int storageAccess, out IPropertyStore propertyStore);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string endpointId);

        [PreserveSig]
        int GetState(out DeviceState state);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channelNumber, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid eventContext);
        [PreserveSig] int VolumeStepDown(ref Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float volumeMindB, out float volumeMaxdB, out float volumeIncrementdB);
    }

    private enum EDataFlow
    {
        Render = 0,
        Capture = 1
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [Flags]
    private enum DeviceState
    {
        Active = 0x1
    }

    private static class StorageAccess
    {
        public const int Read = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        private ushort _variantType;

        [FieldOffset(8)]
        private IntPtr _pointerValue;

        public string? GetValue() =>
            _variantType switch
            {
                31 => Marshal.PtrToStringUni(_pointerValue),
                30 => Marshal.PtrToStringAnsi(_pointerValue),
                _ => null
            };
    }
}
