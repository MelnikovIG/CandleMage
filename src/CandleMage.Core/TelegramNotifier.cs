using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CandleMage.Core;

public interface ITelegramNotifier
{
    Task Send(string text);
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

    public async Task Send(string text)
    {
        _logger.LogInformation($"Send TG message:{Environment.NewLine}" +
                               $"==================================={Environment.NewLine}" +
                               $"{text}{Environment.NewLine}" +
                               $"===================================");

        try
        {
            await _bot.SendTextMessageAsync(
                chatId: new ChatId(_configuration.Value.TelegramChannelId),
                text: text
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send TG message failed");
        }
    }
}