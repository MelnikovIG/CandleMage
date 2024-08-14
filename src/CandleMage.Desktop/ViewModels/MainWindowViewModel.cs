using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CandleMage.Core;
using CandleMage.Desktop.Models;

namespace CandleMage.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private ObservableCollection<Stock> _stocks = new();
    private Dictionary<string, Stock> _assetsDict = new();

    public ObservableCollection<Stock> Stocks
    {
        get => _stocks;
        set => SetProperty(ref _stocks, value);
    }

    public MainWindowViewModel()
    {
        var stocks = new List<Stock>
        {
            new Stock(string.Empty, "Loading...", string.Empty),
        };
        Stocks = new ObservableCollection<Stock>(stocks);
    }

    public void UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets)
    {
        var newAssets = assets.Select(x => new Stock(x.Uid, x.Name, x.Ticker)).ToList();
        
        _assetsDict = newAssets.ToDictionary(x => x.Uid, x => x);
        Stocks = new ObservableCollection<Stock>(newAssets);
    }

    public void UpdateCandleInfo(UpdateCandleInfo candleInfo)
    {
        if (!_assetsDict.TryGetValue(candleInfo.Uid, out var stock))
        {
            return;
        }

        stock.LastPrice = candleInfo.Close;
        stock.LastUpdated = DateTime.Now;
    }
}