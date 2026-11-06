
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using System.Reflection;

namespace TicketPromo;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _printerMenu;
    private readonly Control _uiDispatcher;

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    private bool _isExiting;
    private bool _isChangingSettings;

    public TrayApplicationContext(AppConfig config)
    {
        _config = config;

        _uiDispatcher = new Control();
        _ = _uiDispatcher.Handle;

        string iconPath = Path.Combine(
            AppContext.BaseDirectory,
            "icon.ico");

        if (!File.Exists(iconPath))
            throw new FileNotFoundException(
                "Значок программы для системного трея не найден.",
                iconPath);

        try
        {
            _config.PrinterName = ResolvePrinter(_config.PrinterName);
            _config.Save();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Не удалось настроить принтер: {ex.Message}", ex);
        }

        var contextMenu = new ContextMenuStrip();

        _printerMenu = new ToolStripMenuItem("Принтер");
        UpdatePrinterMenu();

        contextMenu.Items.Add(_printerMenu);

        contextMenu.Items.Add(
            "Выбрать изображение...",
            null,
            async (_, _) => await SelectImageAsync());

        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add(
            "Открыть конфигурацию", 
            null,
            (_, _) => OpenConfig());

        contextMenu.Items.Add(
            "Открыть журнал",
            null,
            (_, _) => OpenLog());

        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add(
            "О программе", 
            null,
            (_, _) => ShowAbout());

        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add(
            "Выход",
            null,
            (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Text = "Ticket Promo",
            Icon = new Icon(iconPath),
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        StartMonitoring();

        if (!File.Exists(_config.ImagePath))
        {
            Notify(
                "Требуется изображение",
                "Файл рекламного изображения не найден. Выберите PNG через меню.",
                NotificationType.Warning);
        }
        else
        {
            Notify(
                "Ticket Promo",
                "Программа успешно запущена и работает в фоновом режиме.");
        }
    }

    private static string ResolvePrinter(string configuredPrinter)
    {
        var installedPrinters = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .ToList();

        if (!string.IsNullOrWhiteSpace(configuredPrinter) &&
            installedPrinters.Contains(
                configuredPrinter,
                StringComparer.OrdinalIgnoreCase) &&
            new PrinterSettings
            {
                PrinterName = configuredPrinter
            }.IsValid)
        {
            return configuredPrinter;
        }

        string defaultPrinter = new PrinterSettings().PrinterName;

        if (!string.IsNullOrWhiteSpace(defaultPrinter) &&
            new PrinterSettings
            {
                PrinterName = defaultPrinter
            }.IsValid)
        {
            return defaultPrinter;
        }

        throw new InvalidOperationException(
            "Не найден ни настроенный принтер, ни принтер по умолчанию.");
    }

    private void UpdatePrinterMenu()
    {
        _printerMenu.DropDownItems.Clear();

        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            var item = new ToolStripMenuItem(printerName)
            {
                Checked = string.Equals(
                    printerName,
                    _config.PrinterName,
                    StringComparison.OrdinalIgnoreCase)
            };

            item.Click += async (_, _) =>
                await SelectPrinterAsync(printerName);

            _printerMenu.DropDownItems.Add(item);
        }

        if (_printerMenu.DropDownItems.Count == 0)
        {
            _printerMenu.DropDownItems.Add(
                new ToolStripMenuItem("Нет доступных принтеров")
                {
                    Enabled = false
                });
        }
    }

    private async Task StopMonitoringAsync()
    {
        _monitorCts?.Cancel();

        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _monitorCts?.Dispose();
        _monitorCts = null;
        _monitorTask = null;
    }

    private async Task SelectPrinterAsync(string printerName)
    {
        if (_isChangingSettings || _isExiting ||
            string.Equals(
                printerName,
                _config.PrinterName,
                StringComparison.OrdinalIgnoreCase))
            return;

        _isChangingSettings = true;

        string previousPrinter = _config.PrinterName;

        try
        {
            if (!new PrinterSettings
            {
                PrinterName = printerName
            }.IsValid)
            {
                throw new InvalidOperationException(
                    $"Принтер '{printerName}' недоступен.");
            }

            await StopMonitoringAsync();

            _config.PrinterName = printerName;
            _config.Save();

            UpdatePrinterMenu();
            StartMonitoring();

            Notify(
                "Ticket Promo",
                $"Принтер изменён на: {printerName}");
        }
        catch (Exception ex)
        {
            _config.PrinterName = previousPrinter;

            try
            {
                _config.Save();
            }
            catch
            {
            }

            UpdatePrinterMenu();

            if (!_isExiting && _monitorCts == null)
                StartMonitoring();

            Notify(
                "Ошибка Ticket Promo",
                $"Не удалось изменить принтер: {ex.Message}",
                NotificationType.Error);
        }
        finally
        {
            _isChangingSettings = false;
        }
    }

    private async Task SelectImageAsync()
    {
        if (_isChangingSettings || _isExiting)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Выберите изображение для печати",
            Filter = "PNG-изображения (*.png)|*.png",
            InitialDirectory = Directory.Exists(
                Path.GetDirectoryName(_config.ImagePath))
                    ? Path.GetDirectoryName(_config.ImagePath)
                    : AppContext.BaseDirectory,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        _isChangingSettings = true;

        string previousImage = _config.ImagePath;

        try
        {
            using (var image = Image.FromFile(dialog.FileName))
            {
                if (image.RawFormat.Guid !=
                    System.Drawing.Imaging.ImageFormat.Png.Guid)
                {
                    throw new InvalidDataException(
                        "Выбранный файл не является изображением PNG.");
                }
            }

            await StopMonitoringAsync();

            _config.ImagePath = dialog.FileName;
            _config.Save();

            StartMonitoring();

            Notify(
                "Ticket Promo",
                "Рекламное изображение успешно изменено.");
        }
        catch (Exception ex)
        {
            _config.ImagePath = previousImage;

            try
            {
                _config.Save();
            }
            catch
            {
            }

            if (!_isExiting && _monitorCts == null)
                StartMonitoring();

            Notify(
                "Ошибка Ticket Promo",
                $"Не удалось изменить изображение: {ex.Message}",
                NotificationType.Error);
        }
        finally
        {
            _isChangingSettings = false;
        }
    }

    private void StartMonitoring()
    {
        if (_isExiting || _monitorCts != null)
            return;

        _monitorCts = new CancellationTokenSource();

        var monitor = new PrinterMonitor(_config);

        monitor.OnLog += Log;

        monitor.OnImageMissing += () =>
        {
            Notify(
                "Требуется изображение",
                "Файл рекламного изображения не найден. Выберите PNG через меню.",
                NotificationType.Warning);
        };

        CancellationToken token = _monitorCts.Token;

        _monitorTask = RunMonitoringAsync(monitor, token);
    }

    private async Task RunMonitoringAsync(
        PrinterMonitor monitor,
        CancellationToken cancellationToken)
    {
        try
        {
            await monitor.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Мониторинг остановлен штатно.
        }
        catch (Exception ex)
        {
            Log($"Критическая ошибка мониторинга: {ex}");

            if (!cancellationToken.IsCancellationRequested)
            {
                Notify(
                    "Критическая ошибка Ticket Promo",
                    ex.Message,
                    NotificationType.Error);
            }
        }
    }

    private void Notify(
        string title,
        string message,
        NotificationType type = NotificationType.Information)
    {
        Log($"[{type}] {title}: {message}");

        if (type == NotificationType.Debug || _isExiting)
            return;

        void Show()
        {
            if (_isExiting || _uiDispatcher.IsDisposed)
                return;

            if (type == NotificationType.Error)
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;

            _notifyIcon.ShowBalloonTip(
                type == NotificationType.Warning ? 5000 : 3000);
        }

        try
        {
            if (_uiDispatcher.IsDisposed ||
                !_uiDispatcher.IsHandleCreated)
                return;

            if (_uiDispatcher.InvokeRequired)
                _uiDispatcher.BeginInvoke((Action)Show);
            else
                Show();
        }
        catch (InvalidOperationException)
        {
            // Приложение закрывается.
        }
    }

    private void Log(string message)
    {
        try
        {
            File.AppendAllText(
                AppPaths.LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ошибка логирования не должна останавливать приложение.
        }
    }

    private void OpenConfig()
    {
        try
        {
            if (!File.Exists(AppPaths.ConfigPath))
            {
                Notify(
                    "Ticket Promo",
                    "Файл конфигурации не найден.",
                    NotificationType.Warning);

                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                UseShellExecute = false,
                ArgumentList = { AppPaths.ConfigPath }
            });
        }
        catch (Exception ex)
        {
            Notify(
                "Ошибка Ticket Promo",
                $"Не удалось открыть конфигурацию: {ex.Message}",
                NotificationType.Error);
        }
    }

    private void OpenLog()
    {
        try
        {
            if (!File.Exists(AppPaths.LogPath))
                File.WriteAllText(AppPaths.LogPath, string.Empty);

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                UseShellExecute = false,
                ArgumentList = { AppPaths.LogPath }
            });
        }
        catch (Exception ex)
        {
            Notify(
                "Ошибка Ticket Promo",
                $"Не удалось открыть журнал: {ex.Message}",
                NotificationType.Error);
        }
    }

    private void ShowAbout()
    {
        Assembly assembly = typeof(TrayApplicationContext).Assembly;

        string description =
             assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
             ?? "Не указано";

        string version =
            assembly.GetName().Version?.ToString(3)
            ?? "Неизвестно";

        string developer =
            assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
            ?? "Не указан";

        MessageBox.Show(
            $"Ticket Promo\n\n" +
            $"{description}\n\n" +
            $"Версия: {version}\n\n" +
            $"© 2026 {developer}\n" +
            "Лицензия: MIT License\n\n" +
            "GitHub: https://github.com/Ivanoka/TicketPromo",
            "О программе",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ExitApplication()
    {
        if (_isExiting)
            return;

        _isExiting = true;

        _monitorCts?.Cancel();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        _uiDispatcher.Dispose();

        _monitorCts?.Dispose();
        _monitorCts = null;

        ExitThread();
    }
}
