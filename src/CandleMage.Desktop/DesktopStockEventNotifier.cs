using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CandleMage.Core;
using CandleMage.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CandleMage.Desktop;

public class DesktopStockEventNotifier : BaseStockEventNotifier
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    public DesktopStockEventNotifier(
        MainWindowViewModel mainWindowViewModel,
        ITelegramNotifier telegramNotifier,
        IOptions<Configuration> configuration,
        ILogger<DesktopStockEventNotifier> logger
    ) : base(telegramNotifier, configuration, logger)
    {
        _mainWindowViewModel = mainWindowViewModel;
    }

    public override async Task UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets)
    {
        await base.UpdateAssetsInfo(assets);
        
        _mainWindowViewModel.UpdateAssetsInfo(assets);
    }

    public override async Task UpdateCandleInfo(UpdateCandleInfo candleInfo)
    {
        await base.UpdateCandleInfo(candleInfo);
        _mainWindowViewModel.UpdateCandleInfo(candleInfo);
    }

    protected override async Task AddEvent(DateTime date, string message)
    {
        await base.AddEvent(date, message);
        
        _mainWindowViewModel.AddEvent(date, message);
    }
}