using SoundGuard.Interop;
using SoundGuard.Models;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Collections.Concurrent;
using static SoundGuard.Interop.NativeMethods;

namespace SoundGuard.Services;

/// <summary>
/// Bluetooth Hands-Free Telephony servisini yöneten ana sınıf.
/// pnputil + CfgMgr32 hibrit yaklaşımıyla HFP cihazlarını listeler ve yönetir.
/// </summary>
public static class HandsFreeServiceManager
{
    /// <summary>
    /// Hands-Free Profile (HFP) servis GUID kısa formu.
    /// Windows BTHENUM formatı: BTHENUM\{0000111E-0000-1000-8000-00805F9B34FB}_...
    /// </summary>
    private const string HFP_GUID_SHORT = "0000111E";


    /// <summary>
    /// Sistemdeki tüm Hands-Free Telephony profiline sahip Bluetooth cihazlarını döndürür.
    /// </summary>
    public static List<BluetoothDeviceInfo> GetHandsFreeDevices()
    {
        var devices = new List<BluetoothDeviceInfo>();

        try
        {
            Log("=== HFP Cihaz Taraması Başladı ===");

            // Yöntem 1: PowerShell ile PnP cihazları sorgula (en güvenilir)
            var pnpDevices = GetHfpDevicesViaPowerShell();
            if (pnpDevices.Count > 0)
            {
                Log($"PowerShell ile {pnpDevices.Count} HFP cihaz bulundu");
                devices.AddRange(pnpDevices);
            }
            else
            {
                Log("PowerShell sonuç vermedi, SetupAPI ile deneniyor...");
                // Yöntem 2: SetupAPI fallback
                var setupApiDevices = GetHfpDevicesViaSetupApi();
                Log($"SetupAPI ile {setupApiDevices.Count} HFP cihaz bulundu");
                devices.AddRange(setupApiDevices);
            }
        }
        catch (Exception ex)
        {
            Log($"HATA: {ex.Message}\n{ex.StackTrace}");
        }

        Log($"Toplam {devices.Count} HFP cihaz döndürülüyor");
        return devices;
    }

    /// <summary>
    /// PowerShell Get-PnpDevice ile HFP cihazlarını bulur.
    /// Bu yöntem tüm sınıflardan bağımsız çalışır ve en güvenilir yöntemdir.
    /// </summary>
    private static List<BluetoothDeviceInfo> GetHfpDevicesViaPowerShell()
    {
        var devices = new List<BluetoothDeviceInfo>();

        try
        {
            // PowerShell komutunu çalıştır
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"Get-PnpDevice | Where-Object { $_.InstanceId -like '*BTHENUM*' -and $_.InstanceId -like '*0000111E*' } | ForEach-Object { $err = $_.ConfigManagerErrorCode; Write-Output \\\"$($_.FriendlyName)|$($_.InstanceId)|$($_.Status)|$err\\\" }\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Log("PowerShell başlatılamadı");
                return devices;
            }

            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);

            Log($"PowerShell çıktısı:\n{output}");
            if (!string.IsNullOrEmpty(errors))
                Log($"PowerShell hataları:\n{errors}");

            if (string.IsNullOrWhiteSpace(output))
                return devices;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Trim().Split('|');
                if (parts.Length < 3) continue;

                string name = parts[0].Trim();
                string instanceId = parts[1].Trim();
                string status = parts[2].Trim();

                // ConfigManagerErrorCode: 0=OK, 22=Disabled
                bool isEnabled = true;
                if (parts.Length >= 4 && int.TryParse(parts[3].Trim(), out int errCode))
                {
                    isEnabled = errCode == 0;
                }
                else
                {
                    isEnabled = status.Equals("OK", StringComparison.OrdinalIgnoreCase);
                }

                Log($"Cihaz: {name} | {instanceId} | Enabled={isEnabled}");

                devices.Add(new BluetoothDeviceInfo
                {
                    Name = CleanDeviceName(name),
                    InstanceId = instanceId,
                    IsEnabled = isEnabled,
                    DeviceAddress = ExtractBluetoothAddress(instanceId)
                });
            }
        }
        catch (Exception ex)
        {
            Log($"PowerShell tarama hatası: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// SetupAPI ile tüm cihaz sınıflarını tarar (fallback).
    /// </summary>
    private static List<BluetoothDeviceInfo> GetHfpDevicesViaSetupApi()
    {
        var devices = new List<BluetoothDeviceInfo>();

        Guid emptyGuid = Guid.Empty;
        IntPtr deviceInfoSet = SetupDiGetClassDevs(
            ref emptyGuid,
            null,
            IntPtr.Zero,
            DIGCF_ALLCLASSES | DIGCF_PRESENT);

        if (deviceInfoSet == INVALID_HANDLE_VALUE)
        {
            Log("SetupDiGetClassDevs başarısız!");
            return devices;
        }

        try
        {
            var devInfoData = new SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
            };

            int scanned = 0;
            for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref devInfoData); i++)
            {
                scanned++;
                char[] buffer = new char[512];
                if (!SetupDiGetDeviceInstanceId(deviceInfoSet, ref devInfoData, buffer, (uint)buffer.Length, out _))
                {
                    devInfoData = new SP_DEVINFO_DATA
                    {
                        cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                    };
                    continue;
                }

                string instanceId = new string(buffer).TrimEnd('\0');

                // BTHENUM + HFP GUID filtresi
                if (instanceId.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase) &&
                    instanceId.Contains(HFP_GUID_SHORT, StringComparison.OrdinalIgnoreCase))
                {
                    string friendlyName = DeviceInterop.GetDeviceProperty(
                        deviceInfoSet, ref devInfoData, SPDRP_FRIENDLYNAME);
                    if (string.IsNullOrEmpty(friendlyName))
                        friendlyName = DeviceInterop.GetDeviceProperty(
                            deviceInfoSet, ref devInfoData, SPDRP_DEVICEDESC);

                    bool isEnabled = DeviceInterop.IsDeviceNodeEnabled(devInfoData.DevInst);

                    Log($"SetupAPI buldu: {friendlyName} | {instanceId} | Enabled={isEnabled}");

                    devices.Add(new BluetoothDeviceInfo
                    {
                        Name = CleanDeviceName(friendlyName),
                        InstanceId = instanceId,
                        IsEnabled = isEnabled,
                        DeviceAddress = ExtractBluetoothAddress(instanceId)
                    });
                }

                devInfoData = new SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                };
            }

            Log($"SetupAPI: toplam {scanned} cihaz tarandı");
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return devices;
    }

    /// <summary>
    /// Belirtilen cihazın Hands-Free servisini etkinleştirir veya devre dışı bırakır.
    /// CfgMgr32 API'lerini kullanır (doğrudan ve güvenilir).
    /// </summary>
    public static bool SetDeviceEnabled(string instanceId, bool enabled)
    {
        try
        {
            Log($"SetDeviceEnabled: {instanceId} -> {(enabled ? "Enable" : "Disable")}");
            bool result = DeviceInterop.EnableDevice(instanceId, enabled);
            Log($"Sonuç: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Log($"Durum değiştirme hatası: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Belirtilen cihazın şu anki etkinlik durumunu sorgular.
    /// </summary>
    public static bool IsDeviceEnabled(string instanceId)
    {
        try
        {
            return DeviceInterop.IsDeviceEnabled(instanceId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cihaz adını temizler — HFP suffix'ini kaldırır.
    /// </summary>
    private static string CleanDeviceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Bilinmeyen Cihaz";

        string trimmed = name.Trim();

        string[] suffixes = [" Hands-Free AG Audio", " Hands-Free AG", " Hands-Free", " HFP"];
        foreach (var suffix in suffixes)
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                string cleaned = trimmed[..^suffix.Length].Trim();
                if (!string.IsNullOrEmpty(cleaned))
                    return cleaned;
            }
        }

        return trimmed;
    }

    /// <summary>
    /// Instance ID'den Bluetooth MAC adresini çıkarır.
    /// </summary>
    private static string ExtractBluetoothAddress(string instanceId)
    {
        try
        {
            if (!instanceId.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var parts = instanceId.Split('&', '\\', '_');
            foreach (var part in parts)
            {
                string cleaned = part.Trim();
                if (cleaned.Length == 12 && cleaned.All(c => "0123456789ABCDEFabcdef".Contains(c)))
                {
                    return string.Join(":",
                        Enumerable.Range(0, 6).Select(i => cleaned.Substring(i * 2, 2)));
                }
            }
        }
        catch { }

        return string.Empty;
    }

    private static void Log(string message)
    {
        Debug.WriteLine($"[SoundGuard] {message}");
    }
}
