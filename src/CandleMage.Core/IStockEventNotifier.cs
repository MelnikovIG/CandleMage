namespace CandleMage.Core;

public interface IStockEventNotifier
{
    Task UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets);
    Task UpdateCandleInfo(UpdateCandleInfo candleInfo);
}

public record UpdateAssetInfo(
    string Uid,
    string Name,
    string Ticker
);

public record UpdateCandleInfo(
    string Uid,
    DateTime CandleStartTime,
    decimal Close
);