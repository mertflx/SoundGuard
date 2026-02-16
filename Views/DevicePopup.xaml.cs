using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SoundGuard.Models;
using SoundGuard.Services;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;

namespace SoundGuard.Views;

/// <summary>
/// Cihaz listesi popup penceresi — sistem tepsisi ikonunun üstünde açılır.
/// </summary>
public partial class DevicePopup : Window
{
    private List<BluetoothDeviceInfo> _devices = [];

    public DevicePopup()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Pencereyi ekranın dışına taşı (ilk yükleme sırasında gizle)
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Kullanıcı başka bir yere tıklarsa popup'ı gizle
        Hide();
    }

    /// <summary>
    /// Sistem tepsisi ikonunun üstünde popup'ı açar.
    /// </summary>
    public void ShowAtTray()
    {
        // Pencereyi göster ve boyutunu hesapla
        Show();
        UpdateLayout();

        // SystemParameters.WorkArea zaten WPF birimi cinsinden
        // ve görev çubuğunu hariç tutar
        var workArea = SystemParameters.WorkArea;

        // Sağ alt köşeye yerleştir
        Left = workArea.Right - ActualWidth - 12;
        Top = workArea.Bottom - ActualHeight - 12;

        // Güvenlik: ekran dışına taşmayı önle
        if (Left < workArea.Left) Left = workArea.Left + 12;
        if (Top < workArea.Top) Top = workArea.Top + 12;

        Activate();
    }

    /// <summary>
    /// Pencereyi çalışma alanı içinde yeniden konumlandırır.
    /// </summary>
    private void RepositionWindow()
    {
        UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 12;
        Top = workArea.Bottom - ActualHeight - 12;
        if (Left < workArea.Left) Left = workArea.Left + 12;
        if (Top < workArea.Top) Top = workArea.Top + 12;
    }

    /// <summary>
    /// Cihaz listesini yeniler.
    /// </summary>
    public async void RefreshDevices()
    {
        DeviceList.Children.Clear();
        EmptyState.Visibility = Visibility.Collapsed;
        LoadingState.Visibility = Visibility.Visible;
        StatusText.Text = "Cihazlar taranıyor...";
        LogToFile("RefreshDevices başladı");

        try
        {
            // Arka planda cihaz taraması yap
            _devices = await Task.Run(() => HandsFreeServiceManager.GetHandsFreeDevices());

            LogToFile($"GetHandsFreeDevices döndü: {_devices.Count} cihaz");

            LoadingState.Visibility = Visibility.Collapsed;

            if (_devices.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                StatusText.Text = "Hands-Free cihaz bulunamadı";
                RepositionWindow();
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;

            int addedCount = 0;
            foreach (var device in _devices)
            {
                try
                {
                    AddDeviceCard(device);
                    addedCount++;
                    LogToFile($"Kart eklendi: {device.Name}");
                }
                catch (Exception cardEx)
                {
                    LogToFile($"Kart ekleme hatası ({device.Name}): {cardEx.Message}\n{cardEx.StackTrace}");
                }
            }

            StatusText.Text = $"{addedCount} cihaz bulundu";
            LogToFile($"UI güncellendi: {addedCount} kart eklendi");

            // İçerik yüklendikten sonra yeniden konumlandır
            RepositionWindow();
        }
        catch (Exception ex)
        {
            LoadingState.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            StatusText.Text = $"Hata: {ex.Message}";
            LogToFile($"RefreshDevices HATA: {ex.Message}\n{ex.StackTrace}");
            RepositionWindow();
        }
    }

    private static void LogToFile(string message)
    {
        Debug.WriteLine($"[SoundGuard.UI] {message}");
    }

    /// <summary>
    /// Bir cihaz için kart düzeni oluşturur ve listeye ekler.
    /// </summary>
    private void AddDeviceCard(BluetoothDeviceInfo device)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(12, 10, 12, 10),
            Cursor = System.Windows.Input.Cursors.Arrow
        };

        // Hover efekti
        card.MouseEnter += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.FromRgb(48, 48, 52));
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Sol taraf: cihaz bilgileri
        var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var nameText = new TextBlock
        {
            Text = device.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220
        };
        infoPanel.Children.Add(nameText);

        // Durum etiketi
        var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };

        var statusDot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = device.IsEnabled
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };
        statusPanel.Children.Add(statusDot);

        var statusText = new TextBlock
        {
            Text = device.IsEnabled ? "Aktif" : "Devre Dışı",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136))
        };
        statusPanel.Children.Add(statusText);

        if (!string.IsNullOrEmpty(device.DeviceAddress))
        {
            var addressText = new TextBlock
            {
                Text = $"  •  {device.DeviceAddress}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };
            statusPanel.Children.Add(addressText);
        }

        infoPanel.Children.Add(statusPanel);
        Grid.SetColumn(infoPanel, 0);
        grid.Children.Add(infoPanel);

        // Sağ taraf: toggle switch
        var toggle = new ToggleButton
        {
            IsChecked = device.IsEnabled,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource("ToggleSwitchStyle"),
            Tag = device
        };
        toggle.Checked += OnToggleChanged;
        toggle.Unchecked += OnToggleChanged;

        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);

        card.Child = grid;
        DeviceList.Children.Add(card);
    }

    /// <summary>
    /// Toggle switch durumu değiştiğinde çağrılır.
    /// Cihazın Hands-Free servisini etkinleştirir veya devre dışı bırakır.
    /// </summary>
    private async void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle || toggle.Tag is not BluetoothDeviceInfo device)
            return;

        bool enable = toggle.IsChecked == true;
        string action = enable ? "Etkinleştiriliyor" : "Devre dışı bırakılıyor";
        StatusText.Text = $"{device.Name} {action}...";
        toggle.IsEnabled = false;

        try
        {
            bool success = await Task.Run(() => HandsFreeServiceManager.SetDeviceEnabled(device.InstanceId, enable));

            if (success)
            {
                device.IsEnabled = enable;
                StatusText.Text = $"{device.Name} {(enable ? "etkinleştirildi ✓" : "devre dışı bırakıldı ✓")}";

                // Durum göstergesini güncelle
                UpdateDeviceCardStatus(device);
            }
            else
            {
                // Başarısız — toggle'ı eski haline döndür
                toggle.Checked -= OnToggleChanged;
                toggle.Unchecked -= OnToggleChanged;
                toggle.IsChecked = !enable;
                toggle.Checked += OnToggleChanged;
                toggle.Unchecked += OnToggleChanged;

                StatusText.Text = $"⚠ {device.Name} değiştirilemedi";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"⚠ Hata: {ex.Message}";
            Debug.WriteLine($"[DevicePopup] Toggle hatası: {ex}");
        }
        finally
        {
            toggle.IsEnabled = true;
        }
    }

    /// <summary>
    /// Cihaz kartındaki durum göstergesini günceller.
    /// </summary>
    private void UpdateDeviceCardStatus(BluetoothDeviceInfo device)
    {
        // Listeyi yeniden oluşturmak yerine mevcut kartları güncelle
        foreach (var child in DeviceList.Children)
        {
            if (child is Border card && card.Child is Grid grid)
            {
                foreach (var gridChild in grid.Children)
                {
                    if (gridChild is ToggleButton toggle && toggle.Tag == device)
                    {
                        // İnfo panelindeki durum etiketini güncelle
                        foreach (var infoChild in grid.Children)
                        {
                            if (infoChild is StackPanel infoPanel && infoPanel.Children.Count >= 2)
                            {
                                if (infoPanel.Children[1] is StackPanel statusPanel && statusPanel.Children.Count >= 2)
                                {
                                    if (statusPanel.Children[0] is Border dot)
                                    {
                                        dot.Background = device.IsEnabled
                                            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                                            : new SolidColorBrush(Color.FromRgb(158, 158, 158));
                                    }
                                    if (statusPanel.Children[1] is TextBlock statusText)
                                    {
                                        statusText.Text = device.IsEnabled ? "Aktif" : "Devre Dışı";
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }
}
