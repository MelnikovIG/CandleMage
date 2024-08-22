using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CandleMage.Desktop.Models;

public record EventModel(DateTime Date,string Message);