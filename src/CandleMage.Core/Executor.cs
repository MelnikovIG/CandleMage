using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;

namespace CandleMage.Core;

public interface IExecutor
{
    Task Run(CancellationToken ct);
}

public class Executor : IExecutor
{
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly ILogger<Executor> _logger;
    private readonly Configuration _configuration;
    private readonly InvestApiClient _investApiClient;

    public Executor(
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger<Executor> logger)
    {
        _telegramNotifier = telegramNotifier;
        _logger = logger;
        _configuration = configuration.Value;
        _investApiClient = InvestApiClientFactory.Create(_configuration.Token, _configuration.Sandbox);
    }

    public async Task Run(CancellationToken ct)
    {
        var assets = (await _investApiClient.Instruments.GetAssetsAsync(new AssetsRequest
                { InstrumentType = InstrumentType.Share }))
            .Assets
            .Where(x => x.Instruments.Count > 0)
            .Select(x => new AssetInfo(
                Uid: x.Instruments.First().Uid,
                Name: x.Name,
                Ticker: x.Instruments.First().Ticker,
                SubscriptionStatus: Status.NotSubscribed,
                MinuteCandles: new ConcurrentDictionary<DateTime, CandleInfo>()
            ))
            .ToList();

        var assetsDict = assets.ToDictionary(x => x.Uid, x => x.Ticker);

        await _telegramNotifier.Send($"Assets count: {assets.Count}");

        while (!ct.IsCancellationRequested)
        {
            StreamLimit? marketStreamLimits;

            try
            {
                const string marketDataStreamName =
                    "tinkoff.public.invest.api.contract.v1.MarketDataStreamService/MarketDataStream";
                var userTariffs = await _investApiClient.Users.GetUserTariffAsync(ct);
                marketStreamLimits =
                    userTariffs.StreamLimits.FirstOrDefault(x => x.Streams.Contains(marketDataStreamName));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get user tariffs");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                continue;
            }

            if (marketStreamLimits == null)
            {
                _logger.LogInformation("marketStreamLimits not found");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                continue;
            }

            var availableStreams = Math.Max(0, marketStreamLimits.Limit - marketStreamLimits.Open);

            var subscribed = 0;
            var notSubscribed = 0;
            var pending = 0;
            foreach (var asset in assets)
            {
                if (asset.SubscriptionStatus == Status.Subscribed) subscribed++;
                if (asset.SubscriptionStatus == Status.NotSubscribed) notSubscribed++;
                if (asset.SubscriptionStatus == Status.Pending) pending++;
            }

            _logger.LogInformation(
                "marketStreamLimits: open '{Open}', limit '{Limit}', available '{Available}'\r\n" +
                "subscription status: subscribed {Subscribed}, not subscribed {NotSubscribed}, pending {Pending}",
                marketStreamLimits.Open, marketStreamLimits.Limit, availableStreams, subscribed, notSubscribed, pending
            );

            await _telegramNotifier.Send(
                $"marketStreamLimits: open '{marketStreamLimits.Open}', limit '{marketStreamLimits.Limit}', available '{availableStreams}'\r\n" +
                $"subscription status: subscribed {subscribed}, not subscribed {notSubscribed}, pending {pending}");

            if (availableStreams == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                continue;
            }

            var notSubscribedAssets =
                assets.Where(x => x.SubscriptionStatus == Status.NotSubscribed).ToList();

            if (notSubscribedAssets.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                continue;
            }
            
            for (int i = 0; i < availableStreams; i++)
            {
                const int maxSteamSize = 300;

                var chunk = notSubscribedAssets
                    .Skip(maxSteamSize * i)
                    .Take(maxSteamSize)
                    .ToList();

                if (chunk.Count == 0)
                {
                    break;
                }

                foreach (var item in chunk)
                {
                    item.SubscriptionStatus = Status.Pending;
                }

                var subscribeData = chunk.Select(x => new CandleInstrument
                {
                    Interval = SubscriptionInterval.OneMinute,
                    InstrumentId = x.Uid
                }).ToList();

                var stream = _investApiClient.MarketDataStream.MarketDataStream();
                Task readStream = ReadStream(stream, chunk, assetsDict, ct);

                await stream.RequestStream.WriteAsync(new MarketDataRequest()
                {
                    SubscribeCandlesRequest = new SubscribeCandlesRequest()
                    {
                        WaitingClose = false,
                        SubscriptionAction = SubscriptionAction.Subscribe,
                        Instruments = { subscribeData }
                    }
                }, cancellationToken: ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private async Task ReadStream(AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> stream,
        List<AssetInfo> chunk, Dictionary<string, string> assetsDict, CancellationToken ct)
    {
        var chunkDict = chunk.ToDictionary(x => x.Uid);
        var subscribedSoFar = new HashSet<string>(chunk.Count);

        try
        {
            await foreach (MarketDataResponse response in stream.ResponseStream.ReadAllAsync(ct))
            {
                if (response.SubscribeCandlesResponse != null)
                {
                    foreach (CandleSubscription candlesSubscription in response.SubscribeCandlesResponse
                                 .CandlesSubscriptions)
                    {
                        if (candlesSubscription.SubscriptionStatus == SubscriptionStatus.Success)
                        {
                            subscribedSoFar.Add(candlesSubscription.InstrumentUid);
                            chunkDict[candlesSubscription.InstrumentUid].SubscriptionStatus = Status.Subscribed;
                        }
                        else
                        {
                            chunkDict[candlesSubscription.InstrumentUid].SubscriptionStatus = Status.NotSubscribed;
                        }
                    }
                }

                if (response.Candle != null)
                {
                    var candle = response.Candle;
                    assetsDict.TryGetValue(candle.InstrumentUid, out var ticker);

                    var assetInfo = chunkDict[candle.InstrumentUid];

                    var candleStartTime = candle.Time.ToDateTime();

                    var candlePeriodNotified = assetInfo.MinuteCandles.TryGetValue(candleStartTime, out var lastCandle) 
                                               && lastCandle.PeriodNotified;
                    
                    var candleInfo = new CandleInfo(
                        candlePeriodNotified,
                        candleStartTime,
                        candle.Open,
                        candle.Close,
                        candle.High,
                        candle.Low
                    );
                    
                    assetInfo.MinuteCandles[candleStartTime] = candleInfo;
                    if (assetInfo.MinuteCandles.Count > 100)
                    {
                        assetInfo.MinuteCandles = new ConcurrentDictionary<DateTime, CandleInfo>(assetInfo.MinuteCandles
                            .OrderByDescending(x => x.Value.StartTime)
                            .Take(50) //shrink to 50
                            .ToDictionary(x => x.Key, x => x.Value));
                    }

                    // _logger.LogInformation("Candle: Ticker '{Ticker}' Figi '{Figi}', Open: '{Open}', Close: '{Close}'",
                    //     ticker, candle.Figi, candle.Open, candle.Close);
                    
                    //Если по последней минутной свече не было нотификаций, проверим изменения
                    if (!candlePeriodNotified)
                    {
                        //Отсортируем свечи по убыванию для поиска изменений по цене
                        var orderedCandles = assetInfo.MinuteCandles.Values
                            .OrderByDescending(x => x.StartTime)
                            .Skip(1) //последняя текущая не нужна
                            .ToList();

                        decimal currentCandlePrice = candle.Close;

                        const decimal priceLimitPercent = 0.0015m; //0.15% //TODO: move to config
                        const int maxScanCandlesCount = 5; //5 min //TODO: move to config

                        var currentScanned = 0;
                        foreach (var orderedCandle in orderedCandles)
                        {
                            var percentDiff = (currentCandlePrice - orderedCandle.Close) / orderedCandle.Close;
                            if (percentDiff is > priceLimitPercent or < -priceLimitPercent)
                            {
                                candleInfo.PeriodNotified = true;

                                _logger.LogInformation(
                                    "Price changed: Ticker '{Ticker}' From {FromPrice} To {ToPrice} In {Minutes} mins, Percent {Percent:N2} %",
                                    ticker, orderedCandle.Close, currentCandlePrice, currentScanned + 1, Math.Abs(percentDiff * 100));

                                var msg = $"Price changed: Ticker '{ticker}' From {orderedCandle.Close} To {currentCandlePrice} In {currentScanned + 1} mins, Percent {Math.Abs(percentDiff * 100):N2} %";
                                await _telegramNotifier.Send(msg);
                                
                                break;
                            }

                            if (++currentScanned == maxScanCandlesCount)
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
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error reading stream");

            foreach (var subscribedUid in subscribedSoFar)
            {
                chunkDict[subscribedUid].SubscriptionStatus = Status.NotSubscribed;
            }
        }

    }

    private enum Status
    {
        NotSubscribed,
        Pending,
        Subscribed
    }

    private record AssetInfo(
        string Uid,
        string Name,
        string Ticker,
        Status SubscriptionStatus,
        ConcurrentDictionary<DateTime, CandleInfo> MinuteCandles
    )
    {
        public Status SubscriptionStatus { get; set; } = SubscriptionStatus;
        
        //TODO: переделать хранение, тут неоптимальное
        public ConcurrentDictionary<DateTime, CandleInfo> MinuteCandles { get; set; } = MinuteCandles;
    }

    private record struct CandleInfo(
        bool PeriodNotified,
        DateTime StartTime,
        decimal Open,
        decimal Close,
        decimal High,
        decimal Low
    );
}
