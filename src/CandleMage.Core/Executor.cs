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
                SubscriptionStatus: Status.NotSubscribed
            ))
            .ToList();

        var assetsDict = assets.ToDictionary(x => x.Uid, x => x.Ticker);

        await _telegramNotifier.SendServiceMessage($"Assets count: {assets.Count}");

        await _stockEventNotifier.UpdateAssetsInfo(
            assets.Select(x => new UpdateAssetInfo(x.Uid, x.Name, x.Ticker)).ToList()
        );
        
        while (!ct.IsCancellationRequested)
        {
            var candlesPerSec = _rpsStopwatch.IsRunning
                ? (_rpsCandlesReceived / _rpsStopwatch.Elapsed.TotalSeconds)
                : 0;
            _rpsCandlesReceived = 0;
            _rpsStopwatch.Restart();
            
            //NOTE: GetUserTariffAsync работает криво, игнорми на данный момент,
            //может показывать меньше чем есть или даже больше лимита
            var availableStreams = 4;

            var subscribed = 0;
            var notSubscribed = 0;
            foreach (var asset in assets)
            {
                if (asset.SubscriptionStatus == Status.Subscribed) subscribed++;
                if (asset.SubscriptionStatus == Status.NotSubscribed) notSubscribed++;
            }
            
            _logger.LogInformation(
                "subscription status: subscribed {Subscribed}, not subscribed {NotSubscribed}, candles/sec {CandlesPerSec:F2}",
                subscribed, notSubscribed, candlesPerSec
            );

            await _telegramNotifier.SendServiceMessage(
                $"subscription status: subscribed {subscribed}, not subscribed {notSubscribed}, candles/sec {candlesPerSec:F2}");
            
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

                    await _stockEventNotifier.UpdateCandleInfo(
                        new UpdateCandleInfo(
                            candle.InstrumentUid,
                            candle.Time.ToDateTime(),
                            candle.Close
                        ));
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
        Status SubscriptionStatus
    )
    {
        public Status SubscriptionStatus { get; set; } = SubscriptionStatus;
    }
}
