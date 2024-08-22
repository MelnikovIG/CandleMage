using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;
using CandleMage.Core;
using CandleMage.Core.Mocks;
using CandleMage.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CandleMage.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole();
        builder.Services.Configure<Configuration>(builder.Configuration.GetSection("Configuration"));
        // builder.Services.AddSingleton<ITelegramNotifier, MockTelegramNotifier>();
        // builder.Services.AddSingleton<IExecutor, MockExecutor>();
        builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
        builder.Services.AddSingleton<IExecutor, Executor>();
        builder.Services.AddSingleton<IStockEventNotifier, DesktopStockEventNotifier>();
        builder.Services.AddSingleton<MainWindowViewModel, MainWindowViewModel>();

        using var host = builder.Build();

        Task.Run(() => host.Services.GetRequiredService<IExecutor>().Run(CancellationToken.None));
        
        BuildAvaloniaApp(host)
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(IHost host)
        => AppBuilder.Configure(() => new App(host))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}