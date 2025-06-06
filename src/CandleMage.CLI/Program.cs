﻿using CandleMage.CLI;
using CandleMage.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("Staring app...");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<HostedService>();
builder.Logging.AddConsole();
builder.Services.Configure<Configuration>(builder.Configuration.GetSection("Configuration"));
// builder.Services.AddSingleton<ITelegramNotifier, MockTelegramNotifier>();
// builder.Services.AddSingleton<IExecutor, MockExecutor>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddSingleton<IExecutor, Executor>();
builder.Services.AddSingleton<IStockEventNotifier, ConsoleStockEventNotifier>();

using var host = builder.Build();
await host.RunAsync();

Console.WriteLine("App closed");
