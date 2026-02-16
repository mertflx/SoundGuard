using System.Runtime.InteropServices;
using System.Text;
using static SoundGuard.Interop.NativeMethods;

namespace SoundGuard.Interop;

/// <summary>
/// SetupAPI native çağrıları üzerine kurulu yönetimli (managed) sarmalayıcılar.
/// Cihaz numaralandırma, durum sorgulama ve etkinleştirme/devre dışı bırakma işlemleri.
/// </summary>
internal static class DeviceInterop
{
    /// <summary>
    /// Belirtilen sınıf GUID'ine ait tüm cihazları döndürür.
    /// Her cihaz için (InstanceId, FriendlyName, DevInst, IsEnabled) bilgisi verir.
    /// </summary>
    public static List<(string InstanceId, string FriendlyName, uint DevInst, bool IsEnabled)> GetDevicesForClass(Guid classGuid)
    {
        var results = new List<(string, string, uint, bool)>();

        IntPtr deviceInfoSet = SetupDiGetClassDevs(
            ref classGuid,
            null,
            IntPtr.Zero,
            DIGCF_PRESENT);

        if (deviceInfoSet == INVALID_HANDLE_VALUE)
            return results;

        try
        {
            var devInfoData = new SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
            };

            for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref devInfoData); i++)
            {
                string instanceId = GetDeviceInstanceId(deviceInfoSet, ref devInfoData);
                if (string.IsNullOrEmpty(instanceId))
                    continue;

                string friendlyName = GetDeviceProperty(deviceInfoSet, ref devInfoData, SPDRP_FRIENDLYNAME);
                if (string.IsNullOrEmpty(friendlyName))
                    friendlyName = GetDeviceProperty(deviceInfoSet, ref devInfoData, SPDRP_DEVICEDESC);
                if (string.IsNullOrEmpty(friendlyName))
                    friendlyName = instanceId;

                bool isEnabled = IsDeviceNodeEnabled(devInfoData.DevInst);

                results.Add((instanceId, friendlyName, devInfoData.DevInst, isEnabled));

                // Struct'ı sıfırla
                devInfoData = new SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                };
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return results;
    }

    /// <summary>
    /// Bir cihazı etkinleştirir veya devre dışı bırakır.
    /// CfgMgr32 API'lerini doğrudan kullanır — daha hızlı ve güvenilirdir.
    /// </summary>
    public static bool EnableDevice(string instanceId, bool enable)
    {
        // Önce cihaz düğümünü (DevNode) bul
        uint ret = CM_Locate_DevNode(out uint devInst, instanceId, CM_LOCATE_DEVNODE_NORMAL);
        
        if (ret != CR_SUCCESS)
        {
            // Belki phantom (takılı değil ama kayıtlı) bir cihazdır?
            ret = CM_Locate_DevNode(out devInst, instanceId, CM_LOCATE_DEVNODE_PHANTOM);
        }

        if (ret != CR_SUCCESS)
            return false;

        // Doğrudan etkinleştir veya devre dışı bırak
        if (enable)
        {
            ret = CM_Enable_DevNode(devInst, 0);
        }
        else
        {
            ret = CM_Disable_DevNode(devInst, 0); // 0 = normal disable
        }

        return ret == CR_SUCCESS;
    }

    /// <summary>
    /// Belirli bir class GUID altında instance ID'ye sahip cihazı bulur ve durumunu değiştirir.
    /// Bu yöntem, cihazın hangi class'a ait olduğunu biliyorsanız daha verimlidir.
    /// </summary>
    public static bool EnableDeviceInClass(Guid classGuid, string instanceId, bool enable)
    {
        IntPtr deviceInfoSet = SetupDiGetClassDevs(
            ref classGuid,
            null,
            IntPtr.Zero,
            DIGCF_PRESENT);

        if (deviceInfoSet == INVALID_HANDLE_VALUE)
            return false;

        try
        {
            var devInfoData = new SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
            };

            for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref devInfoData); i++)
            {
                string currentId = GetDeviceInstanceId(deviceInfoSet, ref devInfoData);
                if (!string.Equals(currentId, instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    devInfoData = new SP_DEVINFO_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                    };
                    continue;
                }

                return ChangeDeviceState(deviceInfoSet, ref devInfoData, enable);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return false;
    }

    /// <summary>
    /// DIF_PROPERTYCHANGE aracılığıyla cihaz durumunu değiştirir.
    /// </summary>
    private static bool ChangeDeviceState(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devInfoData, bool enable)
    {
        var propChangeParams = new SP_PROPCHANGE_PARAMS
        {
            ClassInstallHeader = new SP_CLASSINSTALL_HEADER
            {
                cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(),
                InstallFunction = DIF_PROPERTYCHANGE
            },
            StateChange = enable ? DICS_ENABLE : DICS_DISABLE,
            Scope = DICS_FLAG_GLOBAL,
            HwProfile = 0
        };

        if (!SetupDiSetClassInstallParams(
                deviceInfoSet,
                ref devInfoData,
                ref propChangeParams,
                (uint)Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()))
        {
            return false;
        }

        return SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, deviceInfoSet, ref devInfoData);
    }

    /// <summary>
    /// Cihaz düğümünün çalışır durumda olup olmadığını sorgular.
    /// </summary>
    public static bool IsDeviceNodeEnabled(uint devInst)
    {
        uint result = CM_Get_DevNode_Status(out uint status, out _, devInst, 0);
        if (result != CR_SUCCESS)
            return false;

        return (status & DN_STARTED) != 0;
    }

    /// <summary>
    /// Bir instance ID'ye sahip cihazın çalışır durumda olup olmadığını sorgular.
    /// </summary>
    public static bool IsDeviceEnabled(string instanceId)
    {
        uint result = CM_Locate_DevNode(out uint devInst, instanceId, CM_LOCATE_DEVNODE_NORMAL);
        if (result != CR_SUCCESS)
            return false;

        return IsDeviceNodeEnabled(devInst);
    }

    /// <summary>
    /// Cihaz örnek kimliğini (Instance ID) alır.
    /// </summary>
    private static string GetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devInfoData)
    {
        char[] buffer = new char[512];
        if (SetupDiGetDeviceInstanceId(deviceInfoSet, ref devInfoData, buffer, (uint)buffer.Length, out _))
        {
            return new string(buffer).TrimEnd('\0');
        }
        return string.Empty;
    }

    /// <summary>
    /// Cihaz kayıt defteri özelliğini (registry property) alır.
    /// </summary>
    public static string GetDeviceProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devInfoData, uint property)
    {
        // Önce gerekli buffer boyutunu öğren
        SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref devInfoData,
            property,
            out _,
            null,
            0,
            out uint requiredSize);

        if (requiredSize == 0)
            return string.Empty;

        byte[] propertyBuffer = new byte[requiredSize];
        if (SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref devInfoData,
                property,
                out _,
                propertyBuffer,
                (uint)propertyBuffer.Length,
                out _))
        {
            return Encoding.Unicode.GetString(propertyBuffer).TrimEnd('\0');
        }

        return string.Empty;
    }
}
