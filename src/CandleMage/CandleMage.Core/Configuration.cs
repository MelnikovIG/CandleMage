namespace CandleMage.Core;

public class Configuration
{
    public string Token { get; set; }
    public bool Sandbox { get; set; } = true;
    public string TelegramBotToken { get; set; }
    public string TelegramChannelId { get; set; }
}