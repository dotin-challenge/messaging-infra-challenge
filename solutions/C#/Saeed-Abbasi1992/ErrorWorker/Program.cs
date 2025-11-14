using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;

namespace ErrorWorker;

public class Program
{
    private static async Task Main(string[] args)
    {
        var workerId = $"[ErrorWorker-{Environment.ProcessId}]";

        await using var connection = await new RabbitConnectionHelper().ConnectAsync(AMQPUriHelper.GetAMQP_URI(), Constants.MaxRetryCount, workerId);

        using var cts = new CancellationTokenSource();

        ISubscriber<ErrorMessageModel> subscriber = new ErrorSubscriber(connection, workerId);

        _ = subscriber.SubscribeAsync(cts.Token);

        await Task.Delay(Timeout.Infinite, cts.Token);

        cts.Cancel();
    }
}

