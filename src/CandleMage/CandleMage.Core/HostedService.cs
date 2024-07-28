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
    private readonly ILogger<HostedService> _logger;
    private readonly InvestApiClient _investApiClient;

    public HostedService(
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger<HostedService> logger)
    {
        _configuration = configuration.Value;
        _telegramNotifier = telegramNotifier;
        _logger = logger;

        _investApiClient = InvestApiClientFactory.Create(_configuration.Token, _configuration.Sandbox);
    }
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        
        _logger.LogInformation("Sandbox '{Sandbox}', telegramChannelId '{TelegramChannelId}'",
            _configuration.Sandbox, _configuration.TelegramChannelId);

        await _telegramNotifier.Send("App started");
        
        var assets = await _investApiClient.Instruments.GetAssetsAsync(new AssetsRequest
            { InstrumentType = InstrumentType.Share });
        
        await _telegramNotifier.Send($"Assets count: {assets?.Assets.Count}");
        
        await _telegramNotifier.Send("App complete");
    }
}