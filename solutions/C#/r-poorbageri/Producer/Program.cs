using LoggingLib;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IRabbitLogger>(sp =>
{
    var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
    var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
    var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";
    var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "ProducerService";
    return new RabbitLogger(host, user, pass, serviceName);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapPost("/log/info", (IRabbitLogger logger, string message) =>
{
    logger.Log(message, RabbitLogLevel.Info);
    return Results.Ok("Info log sent");
});

app.MapPost("/log/error", (IRabbitLogger logger, string message) =>
{
    try
    {
        throw new Exception(message);
    }
    catch (Exception ex)
    {
        logger.Log("Error occurred", RabbitLogLevel.Error, ex);
    }
    return Results.Ok("Error log sent");
});

app.Run();

