using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;

namespace CandleMage.Core;

public class HostedService : BackgroundService
{
    private readonly Configuration _configuration;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly ILogger<HostedService> _logger;

    public HostedService(
IHostEnvironment hostEnvironment, 
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger<HostedService> logger)
    {
        _configuration = configuration.Value;
        _telegramNotifier = telegramNotifier;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        
        _logger.LogInformation("Sandbox {Sandbox}, telegramChannelId {TelegramChannelId}",
            _configuration.Sandbox, _configuration.TelegramChannelId);

        await _telegramNotifier.Send("App started");

        await Task.Delay(10_000, ct);
        
        await _telegramNotifier.Send("App complete");
    }
}