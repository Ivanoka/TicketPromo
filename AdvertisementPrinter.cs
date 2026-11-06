
using System.Drawing;
using System.Drawing.Printing;
using System.IO;

namespace TicketPromo;

public sealed class AdvertisementPrinter
{
    private readonly AppConfig _config;

    public AdvertisementPrinter(AppConfig config)
    {
        _config = config;
    }

    public void Print()
    {
        string imagePath = _config.ImagePath;
        string printerName = _config.PrinterName;

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException(
                "Рекламное изображение не найдено.",
                imagePath);
        }

        using var image = Image.FromFile(imagePath);
        using var document = new PrintDocument();

        document.PrinterSettings.PrinterName = printerName;

        if (!document.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException(
                $"Принтер '{printerName}' недоступен.");
        }

        document.PrintController = new StandardPrintController();

        document.DefaultPageSettings.Margins =
            new Margins(0, 0, 0, 0);

        document.PrintPage += (_, e) =>
        {
            Graphics graphics = e.Graphics!;

            graphics.PageUnit = GraphicsUnit.Millimeter;

            float x = _config.MarginLeft;
            float y = _config.MarginTop;

            float width = _config.ImageWidth;
            float height = width * image.Height / image.Width;

            graphics.DrawImage(
                image,
                x,
                y,
                width,
                height);

            e.HasMorePages = false;
        };

        try
        {
            document.Print();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Не удалось напечатать рекламное изображение на принтере '{printerName}'.",
                ex);
        }
    }
}
