using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace CandleMage.Core;

public class HostedService : BackgroundService
{
    private readonly Configuration _configuration;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly IExecutor _executor;
    private readonly ILogger<HostedService> _logger;

    public HostedService(
        ITelegramNotifier telegramNotifier,
        IExecutor executor,
        IOptions<Configuration> configuration,
        ILogger<HostedService> logger)
    {
        _configuration = configuration.Value;
        _telegramNotifier = telegramNotifier;
        _executor = executor;
        _logger = logger;

    }
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        
        _logger.LogInformation("Sandbox '{Sandbox}', telegramChannelId '{TelegramChannelId}'",
            _configuration.Sandbox, _configuration.TelegramChannelId);
        
        await _telegramNotifier.Send("App started");
        
        await _executor.Run(ct);
        
        await _telegramNotifier.Send("App complete");
    }
}