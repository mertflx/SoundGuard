namespace SoundGuard.Models;

/// <summary>
/// Bluetooth Hands-Free profiline sahip bir cihazın bilgilerini tutar.
/// </summary>
public class BluetoothDeviceInfo
{
    /// <summary>Cihazın kullanıcı dostu adı (ör. "Sony WH-1000XM5").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SetupAPI cihaz örnek kimliği (Instance ID).</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Hands-Free Telephony servisinin şu anki durumu.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Cihazın Bluetooth adresi (varsa).</summary>
    public string DeviceAddress { get; set; } = string.Empty;

    public override string ToString() => $"{Name} ({(IsEnabled ? "Açık" : "Kapalı")})";
}
