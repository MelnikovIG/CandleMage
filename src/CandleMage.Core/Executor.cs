using Google.Protobuf.Collections;
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
                SubscriptionStatus: Status.NotSubscribed
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

            for (int i = 0; i < availableStreams; i++)
            {
                const int maxSteamSize = 300;

                var chunk = notSubscribedAssets
                    .Skip(maxSteamSize * i)
                    .Take(maxSteamSize)
                    .ToList();

                foreach (var item in chunk)
                {
                    item.SubscriptionStatus = Status.Pending;
                }

                var subscribeData = chunk.Select(x => new CandleInstrument
                {
                    Interval = SubscriptionInterval.OneHour,
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

                    _logger.LogInformation("Candle: Ticker '{Ticker}' Figi '{Figi}', Open: '{Open}', Close: '{Close}'",
                        ticker, candle.Figi, candle.Open, candle.Close);
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
        Status SubscriptionStatus
    )
    {
        public Status SubscriptionStatus { get; set; } = SubscriptionStatus;
    }
}
