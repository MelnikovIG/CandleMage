### ВНИМАНИЕ!
Проект находится на ранней стадии разработки и не предназначен для использования на данных момент.
Следите за проектом для информации о ходе разрабоки

### Что такое CandleMage
CandleMage - это проект для отслеживания курсов акций через API Тинькофф инвестиций.

![image](https://github.com/user-attachments/assets/76c9a0f8-596e-4f61-a40c-3f4c97ff85c7)

Планируемые фичи:
- [x] Получение списка акций и подписка на стрим свечей через Tinkoff API
- [x] Отслеживание изменений курса акций по цене
- [ ] Отслеживание изменений курса акций по объему
- [x] Конфигурация параметров отслеживания
- [x] Отправка уведомлений об изменениях акций в Telegram

Поддерживаемые патформы:
- [x] Windows (CLI, Desktop)
- [x] Linux (CLI, Desktop)
- [x] Mac (CLI, Desktop)

Способ запуска приложений:
- [x] CLI (консоль)
- [x] Desktop UI (возможно через Avalonia)
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
  
![image](https://github.com/user-attachments/assets/522eb737-f778-4268-a2d5-221ed6d7d9b4)

### Полезные материалы:
* Документация TinkoffAPI https://tinkoff.github.io/investAPI/
* C# SDK repo [new] https://github.com/RussianInvestments/invest-api-csharp-sdk
* C# SDK repo [old] https://github.com/Tinkoff/invest-api-csharp-sdk
* Telegram T-Invest Api Community chat https://t.me/joinchat/VaW05CDzcSdsPULM
