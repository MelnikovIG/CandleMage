namespace CandleMage.Core.Mocks;

/// <summary>
/// Мок для более удобной разработки Desktop приложения без реального соединения к API тинькофф и Telegram
/// </summary>
public class MockTelegramNotifier : ITelegramNotifier
{
    public Task SendClientMessage(string text) => Task.CompletedTask;

    public Task SendServiceMessage(string text) => Task.CompletedTask;
}