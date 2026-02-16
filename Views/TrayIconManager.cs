using System.Drawing;
using System.Windows.Forms;
using SoundGuard.Services;

namespace SoundGuard.Views;

/// <summary>
/// Sistem tepsisi (system tray) ikonu ve sağ-tık menüsünü yönetir.
/// Sol tıklama ile DevicePopup penceresini gösterir.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private DevicePopup? _popup;
    private bool _disposed;

    public TrayIconManager()
    {
        _notifyIcon = new NotifyIcon
        {
            // Updated icon loading and text
            Icon = new Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/Logo.ico"))!.Stream),
            Text = "SoundGuard — Bluetooth HFP Yöneticisi",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.MouseClick += OnTrayIconClick;
    }

    /// <summary>
    /// Tepsi ikonuna sol tıklama — popup'ı göster/gizle.
    /// </summary>
    private void OnTrayIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            TogglePopup();
        }
    }

    /// <summary>
    /// Popup'ı gösterir veya gizler.
    /// </summary>
    private void TogglePopup()
    {
        if (_popup != null && _popup.IsVisible)
        {
            _popup.Hide();
            return;
        }

        _popup ??= new DevicePopup();
        _popup.RefreshDevices();
        _popup.ShowAtTray();
    }

    /// <summary>
    /// Sağ tık bağlam menüsünü oluşturur.
    /// </summary>
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Yenile
        var refreshItem = new ToolStripMenuItem("🔄  Yenile")
        {
            Font = new Font("Segoe UI", 9.5f)
        };
        refreshItem.Click += (_, _) =>
        {
            if (_popup != null && _popup.IsVisible)
            {
                _popup.RefreshDevices();
            }
        };
        menu.Items.Add(refreshItem);

        menu.Items.Add(new ToolStripSeparator());

        // Hakkında
        var aboutItem = new ToolStripMenuItem("ℹ️  Hakkında")
        {
            Font = new Font("Segoe UI", 9.5f)
        };
        aboutItem.Click += (_, _) =>
        {
            System.Windows.MessageBox.Show(
                "SoundGuard v1.0\n\n" +
                "Bluetooth Hands-Free Telephony servisini\n" +
                "yönetmek için hafif bir sistem tepsisi aracı.\n\n" +
                "Windows 10/11 uyumlu.",
                "Hakkında",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        };
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        // Çıkış
        var exitItem = new ToolStripMenuItem("❌  Çıkış")
        {
            Font = new Font("Segoe UI", 9.5f)
        };
        exitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            _popup?.Close();
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        // Menü stilini biraz iyileştir
        menu.BackColor = Color.FromArgb(30, 30, 30);
        menu.ForeColor = Color.White;
        menu.Renderer = new DarkMenuRenderer();

        return menu;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _popup?.Close();
    }
}

/// <summary>
/// Bağlam menüsü için koyu tema renderer'ı.
/// </summary>
internal class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.White;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(Color.FromArgb(55, 55, 65));
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
        else
        {
            using var brush = new SolidBrush(Color.FromArgb(30, 30, 30));
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(60, 60, 60);
    public override Color MenuItemBorder => Color.FromArgb(70, 70, 80);
    public override Color MenuItemSelected => Color.FromArgb(55, 55, 65);
    public override Color MenuStripGradientBegin => Color.FromArgb(30, 30, 30);
    public override Color MenuStripGradientEnd => Color.FromArgb(30, 30, 30);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(55, 55, 65);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(55, 55, 65);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 45, 55);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(45, 45, 55);
    public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 30);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 30);
    public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 30);
    public override Color SeparatorDark => Color.FromArgb(60, 60, 60);
    public override Color SeparatorLight => Color.FromArgb(60, 60, 60);
    public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);
}
