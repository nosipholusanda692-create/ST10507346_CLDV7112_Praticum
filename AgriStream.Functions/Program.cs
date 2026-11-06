using AgriStream.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // (f) Monitoring - Application Insights for the Function App
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Shared Table Storage clients (Telemetry + MediaLog)
        services.AddSingleton<TableStore>();
    })
    .Build();

host.Run();
