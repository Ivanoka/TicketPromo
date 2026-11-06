
using System.Diagnostics;
using System.IO;
using System.Printing;

namespace TicketPromo;

public sealed class PrinterMonitor
{
    private readonly AppConfig _config;
    private readonly AdvertisementPrinter _printer;

    private bool _imageMissingWarningShown;

    public event Action<string>? OnLog;
    public event Action? OnImageMissing;

    public PrinterMonitor(AppConfig config)
    {
        _config = config;
        _printer = new AdvertisementPrinter(config);
    }

    public Task RunAsync(CancellationToken cancellationToken)
    {
        // Выделенный поток: все операции с PrintQueue
        // будут выполняться в одном и том же потоке.
        return Task.Factory.StartNew(
            () => RunCore(cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void RunCore(CancellationToken cancellationToken)
    {
        using var server = new LocalPrintServer();
        using var queue = server.GetPrintQueue(_config.PrinterName);

        OnLog?.Invoke($"Мониторинг принтера: {_config.PrinterName}");

        WaitForQueueIdle(queue, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            WaitForQueueBusy(queue, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            OnLog?.Invoke("Обнаружено задание на печать.");

            WaitForQueueIdle(queue, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(_config.ImagePath))
            {
                OnLog?.Invoke(
                    $"Рекламное изображение не найдено: {_config.ImagePath}");

                if (!_imageMissingWarningShown)
                {
                    _imageMissingWarningShown = true;
                    OnImageMissing?.Invoke();
                }
            }
            else
            {
                _imageMissingWarningShown = false;

                if (!_config.PrintingEnabled)
                {
                    OnLog?.Invoke(
                        "Автоматическая печать отключена. Рекламное задание пропущено.");
                }
                else
                {
                    OnLog?.Invoke("Отправка рекламного изображения на печать...");

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        _printer.Print();

                        OnLog?.Invoke(
                            "Рекламное изображение успешно отправлено на печать.");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke(
                            $"Ошибка печати рекламного изображения: {ex}");
                    }
                }
            }

            WaitForQueueIdle(queue, cancellationToken);

            OnLog?.Invoke("Ожидание следующего задания.");
        }
    }

    private void WaitForQueueBusy(
        PrintQueue queue,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            queue.Refresh();

            if (queue.NumberOfJobs > 0)
                return;

            WaitForNextCheck(cancellationToken);
        }
    }

    private void WaitForQueueIdle(
        PrintQueue queue,
        CancellationToken cancellationToken)
    {
        var idleTimer = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested)
        {
            queue.Refresh();

            if (queue.NumberOfJobs == 0)
            {
                if (idleTimer.ElapsedMilliseconds >= _config.QueueIdleDelay)
                    return;
            }
            else
            {
                idleTimer.Restart();
            }

            WaitForNextCheck(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void WaitForNextCheck(CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(_config.CheckInterval);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
