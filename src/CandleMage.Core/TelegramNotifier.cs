using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
    
    private async Task SendBase(string text, string chatId, ParseMode parseMode = ParseMode.MarkdownV2)
    {
        _logger.LogInformation($"Send TG message:{Environment.NewLine}" +
                               $"==================================={Environment.NewLine}" +
                               $"{text}{Environment.NewLine}" +
                               $"===================================");

        if (parseMode is ParseMode.Markdown or ParseMode.MarkdownV2)
        {
            text = EscapeForMarkdown(text);
        }
        
        try
        {
            await _bot.SendTextMessageAsync(
                chatId: new ChatId(chatId),
                text: text,
                parseMode: parseMode,
                disableWebPagePreview: true
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send TG message failed");
        }
    }

    private static readonly HashSet<char> MarkdownCharsToEscape =
        ['_', '*', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'];
    
    private string EscapeForMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var @char in text)
        {
            if (MarkdownCharsToEscape.Contains(@char))
            {
                sb.Append('\\');
            }
            
            sb.Append(@char);
        }

        return sb.ToString();
    }
}