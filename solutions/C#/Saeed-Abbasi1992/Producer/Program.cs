using Producer;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;

var cts = new CancellationTokenSource();

await using var connection = await new RabbitConnectionHelper().ConnectAsync(AMQPUriHelper.GetAMQP_URI(), SharedKernel.Constants.MaxRetryCount, "Producer");

IPublisher<InfoMessageModel> infoPublisher = new InfoPublisher(connection);
IPublisher<ErrorMessageModel> errorPublisher = new ErrorPublisher(connection);

_ = Task.Run(() => ProduceMessagesAsync(cts.Token));

Console.WriteLine("Press any key to exit...");
Console.ReadLine();

cts.Cancel();

async Task ProduceMessagesAsync(CancellationToken cancellationToken)
{
    var serviceNames = new[] { "billing", "inventory", "notifications", "analytics", "search" };
    var errorMessages = new[] { "Timeout reached", "Failed to connect", "Index out of range", "Memory leak detected", "Transaction aborted" };
    var serverityTyeps = new[] { SeverityType.LOW, SeverityType.MEDIUM, SeverityType.HIGH, SeverityType.CRITICAL };
    var infoMessages = new[] { "GET /api/orders 200", "PATCH /api/users 200", "POST /api/products 202", "DELETE /api/cart 204" };

    for (int index = 1; index <= 500; index++)
    {
        var error = new ErrorMessageModel()
        {
            Id = $"E-{index}",
            Message = errorMessages[index % errorMessages.Length],
            Service = serviceNames[index % serviceNames.Length],
            SeverityType = serverityTyeps[index % serverityTyeps.Length]
        };

        await errorPublisher.PublishAsync(error, cancellationToken);

        var info = new InfoMessageModel()
        {
            Id = $"I-{index}",
            Message = infoMessages[index % infoMessages.Length],
            Service = serviceNames[index % serviceNames.Length],
            Latency = 1000 + (index * 200)
        };

        await infoPublisher.PublishAsync(info, cancellationToken);

        await Task.Delay(500);
    }

    ConsoleLogger.LogInfo("[Producer] Finished publishing messages.");
}
