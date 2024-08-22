namespace CandleMage.Core.Mocks;

/// <summary>
/// Мок для более удобной разработки Desktop приложения без реального соединения к API тинькофф и Telegram
/// </summary>
public class MockExecutor(
    IStockEventNotifier stockEventNotifier
) : IExecutor
{
    public async Task Run(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var assets = new List<UpdateAssetInfo>()
        {
            new("Uid1", "Name1", "Ticker1"),
            new("Uid2", "Name2", "Ticker2"),
            new("Uid3", "Name3", "Ticker3"),
        };

        await stockEventNotifier.UpdateAssetsInfo(assets);

        var random = Random.Shared;

        while (!ct.IsCancellationRequested)
        {
            var delayTimeMs = random.Next(10, 1000);
            var close = (decimal)(10 + random.NextSingle() * 10);
            var asset = assets[random.Next(assets.Count)];

            await Task.Delay(delayTimeMs, ct);
            await stockEventNotifier.UpdateCandleInfo(
                new UpdateCandleInfo(
                    asset.Uid,
                    CandleStartTime: TrimToMinutes(DateTime.Now), //делаем минутную свечу
                    close)
            );
        }
    }
    
    private static DateTime TrimToMinutes(DateTime date) 
        => new(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second % 10);
}