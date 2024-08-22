using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CandleMage.Core;

public abstract class BaseStockEventNotifier : IStockEventNotifier
{
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly ILogger _logger;
    private readonly Configuration _configuration;
    private IReadOnlyList<AssetInfo> _assets = Array.Empty<AssetInfo>();
    private Dictionary<string, AssetInfo> _assetsDict = new();

    protected BaseStockEventNotifier(
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger logger
    )
    {
        _telegramNotifier = telegramNotifier;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public virtual Task UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets)
    {
        _assets = assets.Select(x => new AssetInfo(x.Uid, x.Name, x.Ticker)).ToList();
        _assetsDict = _assets.ToDictionary(x => x.Uid, x => x);
        return Task.CompletedTask;
    }

    public virtual async Task UpdateCandleInfo(UpdateCandleInfo candle)
    {
        _assetsDict.TryGetValue(candle.Uid, out var assetInfo);

        if (assetInfo == null)
        {
            return;
        }

        var ticker = assetInfo.Ticker;
        
        var candleStartTime = candle.CandleStartTime;

        var candlePeriodNotified = assetInfo.MinuteCandles.TryGetValue(candleStartTime, out var lastCandle)
                                   && lastCandle.PeriodNotified;

        var candleInfo = new CandleInfo(
            candlePeriodNotified,
            candleStartTime,
            candle.Close
        );

        assetInfo.MinuteCandles[candleStartTime] = candleInfo;
        if (assetInfo.MinuteCandles.Count > 100)
        {
            assetInfo.MinuteCandles = new ConcurrentDictionary<DateTime, CandleInfo>(assetInfo.MinuteCandles
                .OrderByDescending(x => x.Value.StartTime)
                .Take(60) //shrink to 60
                .ToDictionary(x => x.Key, x => x.Value));
        }

        //Если по последней минутной свече не было нотификаций, проверим изменения
        if (!candlePeriodNotified)
        {
            //Отсортируем свечи по убыванию для поиска изменений по цене
            var orderedCandles = assetInfo.MinuteCandles.Values
                .OrderByDescending(x => x.StartTime)
                .Skip(1) //последняя текущая не нужна
                .ToList();

            decimal currentCandlePrice = candle.Close;

            decimal notifyChangePercentThreshold = _configuration.NotifyChangePercentThreshold;
            int changeMinutesThreshold = _configuration.NotifyChangeMinutesThreshold;

            var currentScanned = 0;
            foreach (var orderedCandle in orderedCandles)
            {
                var percentDiff = (currentCandlePrice - orderedCandle.Close) / orderedCandle.Close;
                if (percentDiff > notifyChangePercentThreshold || percentDiff < -notifyChangePercentThreshold)
                {
                    candleInfo.PeriodNotified = true;

                    var indicator = percentDiff > notifyChangePercentThreshold ? "🟢" : "🔴";

                    _logger.LogInformation(
                        @"{Indicator} {Percent:N2}% '{Ticker}' {FromPrice} → {ToPrice} за {Minutes} мин",
                        indicator, Math.Abs(percentDiff * 100), ticker, orderedCandle.Close, currentCandlePrice,
                        currentScanned + 1);

                    var msg =
                        $@"{indicator} {Math.Abs(percentDiff * 100):N2}% [{ticker}]({GetTickerLink(ticker)}) {orderedCandle.Close} → {currentCandlePrice} за {currentScanned + 1} мин";
                    await _telegramNotifier.SendClientMessage(msg);

                    await AddEvent(DateTime.Now, $@"{indicator} {Math.Abs(percentDiff * 100):N2}% {ticker} {orderedCandle.Close} → {currentCandlePrice} за {currentScanned + 1} мин");
                    
                    break;
                }

                if (++currentScanned == changeMinutesThreshold)
                {
                    break;
                }

                if (orderedCandle.PeriodNotified) //Если у какой то свечи была нотификация, до нее свечи не проверяем
                {
                    break;
                }
            }
        }
    }

    protected virtual Task AddEvent(DateTime date, string message)
    {
        return Task.CompletedTask;
    }
    
    private static string GetTickerLink(string? ticker) => $"https://www.tinkoff.ru/invest/stocks/{ticker}/";

    private record AssetInfo(
        string Uid,
        string Name,
        string Ticker
    )
    {
        public ConcurrentDictionary<DateTime, CandleInfo> MinuteCandles { get; set; } = new();
    }
    
    private record CandleInfo(
        bool PeriodNotified,
        DateTime StartTime,
        decimal Close
    )
    {
        public bool PeriodNotified { get; set; } = PeriodNotified;
    }
}