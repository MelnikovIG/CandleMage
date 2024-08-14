using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CandleMage.Desktop.Models;

public class Stock (string Uid, string Name, string Ticker): ObservableObject
{
    public string Uid { get; init; } = Uid;
    public string Name { get; init; } = Name;
    public string Ticker { get; init; } = Ticker;

    private decimal? _lastPrice;
    public decimal? LastPrice
    {
        get => _lastPrice;
        set => SetProperty(ref _lastPrice, value);
    }
    
    private DateTime? _lastUpdated;
    public DateTime? LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }
}