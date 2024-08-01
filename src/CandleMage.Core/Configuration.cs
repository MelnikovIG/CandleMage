namespace CandleMage.Core;

public class Configuration
{
    public string Token { get; set; } = null!;
    public bool Sandbox { get; set; } = true;
    public string TelegramBotToken { get; set; } = null!;
    public string TelegramClientChannelId { get; set; } = null!;
    public string TelegramServiceChannelId { get; set; } = null!;
}