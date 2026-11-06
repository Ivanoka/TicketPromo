
using System.IO;
using System.Text.Json;

namespace TicketPromo;

public sealed class AppConfig
{
    public string PrinterName { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public bool PrintingEnabled { get; set; } = true;
    public int CheckInterval { get; set; } = 50;
    public int QueueIdleDelay { get; set; } = 500;
    public float ImageWidth { get; set; } = 70;
    public float MarginLeft { get; set; } = 0;
    public float MarginTop { get; set; } = 0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static AppConfig Load()
    {
        string basePath = AppContext.BaseDirectory;

        if (!File.Exists(AppPaths.ConfigPath))
        {
            throw new FileNotFoundException(
                "Файл конфигурации не найден.",
                AppPaths.ConfigPath);
        }

        string json = File.ReadAllText(AppPaths.ConfigPath);

        var config = JsonSerializer.Deserialize<AppConfig>(
            json,
            JsonOptions);

        if (config == null)
        {
            throw new InvalidDataException(
                "Некорректный файл конфигурации.");
        }

        // PrinterName может быть пустым:
        // в этом случае будет выбран принтер Windows по умолчанию.

        if (string.IsNullOrWhiteSpace(config.ImagePath))
        {
            throw new InvalidDataException(
                "Путь к рекламному изображению (ImagePath) не указан.");
        }

        if (config.CheckInterval <= 0)
        {
            throw new InvalidDataException(
                "Интервал проверки (CheckInterval) должен быть положительным.");
        }

        if (config.QueueIdleDelay < 0)
        {
            throw new InvalidDataException(
                "Задержка очереди (QueueIdleDelay) не может быть отрицательной.");
        }

        if (config.ImageWidth < 0)
        {
            throw new InvalidDataException(
                "Ширина картинки (ImageWidth) не может быть отрицательной.");
        }

        if (config.MarginTop < 0)
        {
            throw new InvalidDataException(
                "Отступ печати (MarginTop) не может быть отрицательным.");
        }

        if (config.MarginLeft < 0)
        {
            throw new InvalidDataException(
                "Отступ печати (MarginLeft) не может быть отрицательным.");
        }

        // Преобразуем относительный путь в абсолютный.
        if (!Path.IsPathRooted(config.ImagePath))
        {
            config.ImagePath = Path.GetFullPath(
                Path.Combine(basePath, config.ImagePath));
        }
        else
        {
            config.ImagePath = Path.GetFullPath(config.ImagePath);
        }

        // Отсутствие изображения больше не препятствует запуску.

        return config;
    }

    public void Save()
    {
        AppPaths.EnsureDirectories();

        ImagePath = Path.GetFullPath(ImagePath);

        var json = JsonSerializer.Serialize(this, JsonOptions);

        File.WriteAllText(AppPaths.ConfigPath, json);
    }
}
