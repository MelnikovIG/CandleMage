namespace CandleMage.Core;

public class Configuration
{
    public required string Token { get; set; }
    public required bool Sandbox { get; set; }
    public required string TelegramBotToken { get; set; }
    public required string TelegramClientChannelId { get; set; }
    public required string TelegramServiceChannelId { get; set; }
    public required decimal NotifyChangePercentThreshold { get; set; }
    public required int NotifyChangeMinutesThreshold { get; set; }
}