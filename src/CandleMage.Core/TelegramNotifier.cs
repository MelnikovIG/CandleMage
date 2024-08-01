using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CandleMage.Core;

public interface ITelegramNotifier
{
    Task SendClientMessage(string text);
    Task SendServiceMessage(string text);
}

public class TelegramNotifier : ITelegramNotifier
{
    private readonly IOptions<Configuration> _configuration;
    private readonly ILogger<TelegramNotifier> _logger;
    private readonly TelegramBotClient _bot;

    public TelegramNotifier(
        IOptions<Configuration> configuration,
        ILogger<TelegramNotifier> logger
    )
    {
        _configuration = configuration;
        _logger = logger;
        _bot = new TelegramBotClient(_configuration.Value.TelegramBotToken);
    }

    public async Task SendClientMessage(string text)
    {
        await SendBase(text, _configuration.Value.TelegramClientChannelId);
    }
    
    public async Task SendServiceMessage(string text)
    {
        await SendBase(text, _configuration.Value.TelegramServiceChannelId);
    }
    
    private async Task SendBase(string text, string chatId)
    {
        _logger.LogInformation($"Send TG message:{Environment.NewLine}" +
                               $"==================================={Environment.NewLine}" +
                               $"{text}{Environment.NewLine}" +
                               $"===================================");

        try
        {
            await _bot.SendTextMessageAsync(
                chatId: new ChatId(chatId),
                text: text
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send TG message failed");
        }
    }
}