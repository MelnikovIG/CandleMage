using System.Collections.Generic;
using System.Threading.Tasks;
using CandleMage.Core;
using CandleMage.Desktop.ViewModels;

namespace CandleMage.Desktop;

public class DesktopStockEventNotifier : IStockEventNotifier
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    public DesktopStockEventNotifier(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public void UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets)
    {
        _mainWindowViewModel.UpdateAssetsInfo(assets);
    }

    public void UpdateCandleInfo(UpdateCandleInfo candleInfo)
    {
        _mainWindowViewModel.UpdateCandleInfo(candleInfo);
    }
}