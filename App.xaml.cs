using System.Security.Principal;
using System.Windows;
using SoundGuard.Views;
using MessageBox = System.Windows.MessageBox;

namespace SoundGuard;

/// <summary>
/// Uygulama giriş noktası.
/// Yönetici izni kontrolü yapar ve sistem tepsisi ikonunu başlatır.
/// </summary>
public partial class App : System.Windows.Application
{
    private TrayIconManager? _trayIconManager;
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Tek örnek kontrolü (Single Instance)
        _mutex = new Mutex(true, "SoundGuard_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "SoundGuard zaten çalışıyor.\nSistem tepsisindeki ikonu kontrol edin.",
                "SoundGuard",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Yönetici izni kontrolü
        if (!IsRunningAsAdmin())
        {
            MessageBox.Show(
                "SoundGuard, Bluetooth cihaz servislerini yönetebilmek için\n" +
                "Yönetici (Administrator) izniyle çalıştırılmalıdır.\n\n" +
                "Lütfen uygulamayı sağ tıklayıp \"Yönetici olarak çalıştır\" seçeneği ile başlatın.",
                "Yönetici İzni Gerekli",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // Sistem tepsisi ikonunu başlat
        _trayIconManager = new TrayIconManager();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Uygulamanın yönetici haklarıyla çalışıp çalışmadığını kontrol eder.
    /// </summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
