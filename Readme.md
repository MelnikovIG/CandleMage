### ВНИМАНИЕ!
Проект находится на ранней стадии разработки и не предназначен для использования на данных момент.
Следите за проектом для информации о ходе разрабоки

### Что такое CandleMage
CandleMage - это проект для отслеживания курсов акций через API Тинькофф инвестиций.

Планируемые фичи:
- [x] Получение списка акций и подписка на стрим свечей через Tinkoff API
- [x] Отслеживание изменений курса акций по цене
- [ ] Отслеживание изменений курса акций по объему
- [x] Конфигурация параметров отслеживания
- [x] Отправка уведомлений об изменениях акций в Telegram

Поддерживаемые патформы:
- [x] Windows (CLI)
- [x] Linux (CLI)
- [ ] Mac

Способ запуска приложений:
- [x] CLI (консоль)
- [ ] Desktop UI (возможно через Avalonia)
- [ ] Web (пока под вопросом)

### Ссылки
* Телеграм канал (туда будут прходит уведомления как пример работы бота, когда будет доделана CLI часть) https://t.me/CandleMage
* Телеграм чат для общения https://t.me/CandleMageChat

### Разработка и сборка проекта
* Необходим .NET 8

### Создание и получение токена TinkoffApi:
* https://www.tbank.ru/invest/settings/api/

### Настройка уведомлений в телеграм бота:
* Создайте своего бота через https://t.me/BotFather
* Создайте свой канал и узнайте его Id (например через добавления бота @RawDataBot в канал)
* Добавьте созданного бота в созданный канал

### Полезные материалы:
* Документация TinkoffAPI https://tinkoff.github.io/investAPI/
* C# SDK repo [new] https://github.com/RussianInvestments/invest-api-csharp-sdk
* C# SDK repo [old] https://github.com/Tinkoff/invest-api-csharp-sdk
* Telegram T-Invest Api Community chat https://t.me/joinchat/VaW05CDzcSdsPULM