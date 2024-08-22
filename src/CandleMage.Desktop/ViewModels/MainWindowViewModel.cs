using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CandleMage.Core;
using CandleMage.Desktop.Models;

namespace CandleMage.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private Dictionary<string, StockModel> _assetsDict = new();

    private ObservableCollection<StockModel> _stocks = new();
    public ObservableCollection<StockModel> Stocks
    {
        get => _stocks;
        set => SetProperty(ref _stocks, value);
    }
    
    private ObservableCollection<EventModel> _events = new();
    public ObservableCollection<EventModel> Events
    {
        get => _events;
        set => SetProperty(ref _events, value);
    }

    public MainWindowViewModel()
    {
        var stocks = new List<StockModel>
        {
            new StockModel(string.Empty, "Loading...", string.Empty),
        };
        Stocks = new ObservableCollection<StockModel>(stocks);
    }

    public void UpdateAssetsInfo(IReadOnlyList<UpdateAssetInfo> assets)
    {
        var newAssets = assets.Select(x => new StockModel(x.Uid, x.Name, x.Ticker)).ToList();
        
        _assetsDict = newAssets.ToDictionary(x => x.Uid, x => x);
        Stocks = new ObservableCollection<StockModel>(newAssets);
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

    public void AddEvent(DateTime date, string message)
    {
        Dispatcher.UIThread.Invoke(
            () => Events.Add(
                new EventModel(date, message)
            )
        );
    }
}