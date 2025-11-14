using InfoSubscriber;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;

public class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please pass a service name as argument, e.g., grafana or elk");
            return;
        }

        var serviceName = args[0];

        await using var connection = await new RabbitConnectionHelper().ConnectAsync(AMQPUriHelper.GetAMQP_URI(), Constants.MaxRetryCount, serviceName);

        using var cts = new CancellationTokenSource();

        ISubscriber<InfoMessageModel> subscriber = new LogInfoSubscriber(connection, serviceName);

        _ = subscriber.SubscribeAsync(cts.Token);

        await Task.Delay(Timeout.Infinite, cts.Token);

        cts.Cancel();
    }
}