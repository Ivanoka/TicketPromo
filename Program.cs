
using System.IO;
using System.Windows.Forms;

namespace TicketPromo;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        bool createdNew;

        _mutex = new Mutex(
            initiallyOwned: true,
            name: @"Global\TicketPromo",
            createdNew: out createdNew);

        if (!createdNew)
        {
            _mutex.Dispose();
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();

            AppConfig config = AppConfig.Load();

            Application.Run(new TrayApplicationContext(config));
        }
        catch (Exception ex)
        {
            LogStartupError(ex);

            MessageBox.Show(
                $"Не удалось запустить Ticket Promo.\n\n{ex.Message}",
                "Ticket Promo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    private static void LogStartupError(Exception exception)
    {
        try
        {
            File.AppendAllText(
                AppPaths.LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Ошибка запуска:{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ошибка журналирования не должна препятствовать показу сообщения.
        }
    }
}
