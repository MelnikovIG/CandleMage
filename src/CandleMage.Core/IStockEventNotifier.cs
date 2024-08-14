namespace CandleMage.Core;

public interface IStockEventNotifier
{
    void UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets);
    void UpdateCandleInfo(UpdateCandleInfo candleInfo);
}

public record UpdateAssetInfo(
    string Uid,
    string Name,
    string Ticker
);

public record UpdateCandleInfo(
    string Uid,
    decimal Close
);