using System.IO;

namespace TicketPromo;

public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Ticket Promo");

    public static string ConfigPath =>
        Path.Combine(DataDirectory, "config.json");

    public static string LogPath =>
        Path.Combine(DataDirectory, "ticketpromo.log");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
    }
}