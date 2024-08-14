using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CandleMage.Desktop.ViewModels;
using CandleMage.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CandleMage.Desktop;

public partial class App : Application
{
    private readonly IHost _host;

    public App(IHost host)
    {
        _host = host;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            desktop.MainWindow = new MainWindow
            {
                DataContext = _host.Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}