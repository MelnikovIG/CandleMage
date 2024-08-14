using System.Collections.Concurrent;
using System.Diagnostics;
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
    private readonly IStockEventNotifier _stockEventNotifier;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly ILogger<Executor> _logger;
    private readonly Configuration _configuration;
    private readonly InvestApiClient _investApiClient;
    
    private long _rpsCandlesReceived = 0;
    private readonly Stopwatch _rpsStopwatch = new Stopwatch();

    public Executor(
        IStockEventNotifier stockEventNotifier,
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger<Executor> logger)
    {
        _stockEventNotifier = stockEventNotifier;
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

        await _telegramNotifier.SendServiceMessage($"Assets count: {assets.Count}");

        _stockEventNotifier.UpdateAssetsInfo(
            assets.Select(x => new UpdateAssetInfo(x.Uid, x.Name, x.Ticker)).ToList()
        );
        
        while (!ct.IsCancellationRequested)
        {
            var candlesPerSec = _rpsStopwatch.IsRunning
                ? (int)(_rpsCandlesReceived / _rpsStopwatch.Elapsed.TotalSeconds)
                : 0;
            _rpsCandlesReceived = 0;
            _rpsStopwatch.Restart();
            
            // StreamLimit? marketStreamLimits;
            //
            // try
            // {
            //     const string marketDataStreamName =
            //         "tinkoff.public.invest.api.contract.v1.MarketDataStreamService/MarketDataStream";
            //     var userTariffs = await _investApiClient.Users.GetUserTariffAsync(ct);
            //     marketStreamLimits =
            //         userTariffs.StreamLimits.FirstOrDefault(x => x.Streams.Contains(marketDataStreamName));
            // }
            // catch (Exception e)
            // {
            //     _logger.LogError(e, "Failed to get user tariffs");
            //     await Task.Delay(TimeSpan.FromSeconds(60), ct);
            //     continue;
            // }
            //
            // if (marketStreamLimits == null)
            // {
            //     _logger.LogInformation("marketStreamLimits not found");
            //     await Task.Delay(TimeSpan.FromSeconds(60), ct);
            //     continue;
            // }
            //
            // var openStreams = marketStreamLimits.Open;
            // var limitStreams = marketStreamLimits.Limit;
            // var availableStreams = Math.Max(0, marketStreamLimits.Limit - marketStreamLimits.Open);
            
            //NOTE: GetUserTariffAsync работает криво, игнорми на данный момент
            var openStreams = -1;
            var limitStreams = -1;
            var availableStreams = 4;

            var subscribed = 0;
            var notSubscribed = 0;
            foreach (var asset in assets)
            {
                if (asset.SubscriptionStatus == Status.Subscribed) subscribed++;
                if (asset.SubscriptionStatus == Status.NotSubscribed) notSubscribed++;
            }

            _logger.LogInformation(
                "marketStreamLimits: open '{Open}', limit '{Limit}', available '{Available}'\r\n" +
                "subscription status: subscribed {Subscribed}, not subscribed {NotSubscribed}, candles/sec {CandlesPerSec}",
                openStreams, limitStreams, availableStreams, subscribed, notSubscribed, candlesPerSec
            );

            await _telegramNotifier.SendServiceMessage(
                $"marketStreamLimits: open '{openStreams}', limit '{limitStreams}', available '{availableStreams}'\r\n" +
                $"subscription status: subscribed {subscribed}, not subscribed {notSubscribed}, candles/sec {candlesPerSec}");

            if (availableStreams == 0)
            {
                if (subscribed > 0)
                    await Task.Delay(TimeSpan.FromSeconds(60), ct);
                else
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);

                continue;
            }

            var notSubscribedAssets =
                assets.Where(x => x.SubscriptionStatus == Status.NotSubscribed).ToList();

            if (notSubscribedAssets.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(60), ct);
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
                    Interlocked.Increment(ref _rpsCandlesReceived);
                    
                    var candle = response.Candle;

                    _stockEventNotifier.UpdateCandleInfo(
                        new UpdateCandleInfo(
                            candle.InstrumentUid,
                            candle.Close
                        ));
                    
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
                                    indicator, Math.Abs(percentDiff * 100), ticker, orderedCandle.Close, currentCandlePrice, currentScanned + 1);

                                var msg = $@"{indicator} {Math.Abs(percentDiff * 100):N2}% [{ticker}]({GetTickerLink(ticker)}) {orderedCandle.Close} → {currentCandlePrice} за {currentScanned + 1} мин";
                                await _telegramNotifier.SendClientMessage(msg);
                                
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
            }
        }
        catch (Exception)
        {
            _logger.LogError("Error reading stream");

            foreach (var subscribedUid in subscribedSoFar)
            {
                chunkDict[subscribedUid].SubscriptionStatus = Status.NotSubscribed;
            }
        }

    }

    private enum Status
    {
        NotSubscribed,
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

    private record CandleInfo(
        bool PeriodNotified,
        DateTime StartTime,
        decimal Open,
        decimal Close,
        decimal High,
        decimal Low
    )
    {
        public bool PeriodNotified { get; set; } = PeriodNotified;
    }

    private static string GetTickerLink(string? ticker) => $"https://www.tinkoff.ru/invest/stocks/{ticker}/";
}
