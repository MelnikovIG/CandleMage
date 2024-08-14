using CandleMage.Core;

namespace CandleMage.CLI;

public class ConsoleStockEventNotifier : IStockEventNotifier
{
    public void UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets)
    {
    }

    public void UpdateCandleInfo(UpdateCandleInfo candleInfo)
    {
    }
}