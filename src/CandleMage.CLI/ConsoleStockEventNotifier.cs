using CandleMage.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CandleMage.CLI;

public class ConsoleStockEventNotifier : BaseStockEventNotifier
{
    public ConsoleStockEventNotifier(
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger<ConsoleStockEventNotifier> logger
    ) : base(telegramNotifier, configuration, logger)
    {
    }
}