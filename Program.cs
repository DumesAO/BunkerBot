using BunkerBot;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var db = new AppDbContext();
db.Database.EnsureCreated();

Regex regex = new(@"(.+)_(.+)", RegexOptions.Compiled);

var botClient = new TelegramBotClient("6214939544:AAHLXs-my1VbqlZbgqmFSFSOXCrGZkSzJA0");
using CancellationTokenSource cts = new();
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};
botClient.StartReceiving(
updateHandler: HandleUpdateAsync,
pollingErrorHandler: HandlePollingErrorAsync,
receiverOptions: receiverOptions,
cancellationToken: cts.Token
);
var me = await botClient.GetMeAsync();


Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();
cts.Cancel();
async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message)
    {
        var message = update.Message;
        if (update.Message.Chat.Type == ChatType.Private)
        {
            var messageSplited = message.Text.Split(new string[] { "chatId=" }, StringSplitOptions.None);
            var chatId = message.Chat.Id;
            if (messageSplited.Length > 1)
            {
                var gameChatId = long.Parse(messageSplited[1]);
                var chatTitle = botClient.GetChatAsync(gameChatId).Result.Title;
                if (await JoinUser(gameChatId, chatId))
                {
                    Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Ти успішно приєднався до гри в чаті \"'{chatTitle}'\"",
                cancellationToken: cancellationToken);
                }
                Console.WriteLine($"Received a '{message.Text}' message in chat {chatId}.");
            }
            else
            {
                switch (message.Text.Trim())
                {
                    case "/start":
                        {
                            Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Привіт. Для того щоб почати гру, додай мене в чат своїх друзів та надай права Адміністратора",
                                cancellationToken: cancellationToken);


                        }
                        break;
                    case "/leave":
                        {
                            if (LeaveUser(chatId))
                            {
                                Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Успішно покинуто гру",
                                cancellationToken: cancellationToken);
                            }
                            else
                            {
                                Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Ви зараз не знаходитесь в грі",
                                cancellationToken: cancellationToken);
                            }
                        }
                        break;
                    default:
                        {
                            Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Команду не знайдено",
                                cancellationToken: cancellationToken);
                        }
                        break;
                }
                db.SaveChanges();
            }

        }

        if (update.Message.Chat.Type == ChatType.Supergroup || update.Message.Chat.Type == ChatType.Group)
        {
            var chatId = update.Message.Chat.Id;
            switch (message.Text.Trim())
            {
                case "/createGame":
                    {
                        CreateGame(chatId, botClient, cancellationToken, message);
                    }
                    break;
                case "/startGame":
                    {
                        StartGame(chatId, botClient, cancellationToken);
                    }
                    break;
                default:
                    break;
                case "/stopGame":
                    {
                        StopGame(chatId, botClient, cancellationToken);
                    }
                    break;
            }



            db.SaveChanges();

        }
    }

}
Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

async Task<bool> JoinUser(long chatId, long userId)
{
    foreach (BGame game in db.Games)
    {
        if (game.Users != null)
        {
            foreach (BUser user in game.Users)
            {
                if (user.TelegramId == userId)
                {
                    return false;
                }
            }
        }
    }
    foreach (BGame game in db.Games)
    {
        if (game.GroupId == chatId)
        {
            if (game.Status != 0)
            {
                return false;
            }
            var user = db.Users.FirstOrDefault(d => d.TelegramId == userId);
            if (user == null)
            {
                user = await AddUser(userId);
            }
            if (game.Users.Count == 0)
                game.Admin = user;
            user.BGame = game;
            db.SaveChanges();
            return true;
        }
    }
    return false;
}

bool LeaveUser(long chatId)
{
    BUser user = GetUser(chatId);
    if (user == null)
        return false;
    if (user.BGame == null)
        return false;
    BGame game = user.BGame;
    game.Users.Remove(user);
    user.BGame = null;
    db.SaveChanges();
    return true;
}

async Task<BUser> AddUser(long userId)
{
    BUser user = new BUser();
    user.TelegramId = userId;
    var chat = await botClient.GetChatAsync(userId);
    user.Name = chat.FirstName;
    db.Users.Add(user);
    db.SaveChanges();
    return user;
}
async void CreateGame(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken, Message message)
{
    BGame existingGame = GetGame(chatId);
    if (existingGame != null)
    {
        Message sent1Message = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"В даному чаті вже є гра в процесі",
                                 cancellationToken: cancellationToken);
        return;
    }
    BGame game = new BGame();
    game.GroupId = chatId;
    db.Games.Add(game);
    db.SaveChanges();

    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithUrl("Приєднатися",$"https://t.me/{botClient.GetMeAsync().Result.Username}?start=chatId={chatId}"),
                    });
    Message sentMessage = await botClient.SendTextMessageAsync(
              chatId: chatId,
              text: $"'{message.From.Username}' почав гру",
              replyMarkup: inlineKeyboard,
              cancellationToken: cancellationToken);
    game.StartGameBotMessageId = sentMessage.MessageId;
    db.SaveChanges();
}
async void StopGame(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame game = GetGame(chatId);
    if (game == null)
    {
        Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"В даному чаті створену гру не знайдено",
                                 cancellationToken: cancellationToken);
        return;
    }

    foreach (BUser user in game.Users)
    {
        user.AdditionalInfoOpened = false;
        user.ProfessionOpened = false;
        user.BiologyOpened = false;
        user.FirstSpecialCardUsed = false;
        user.HealthConditionOpened = false;
        user.HobbyOpened = false;
        user.LuggagesOpened = false;
        user.SecondSpecialCardUsed = false;
        user.Profession = null;
        user.Biology = null;
        user.HealthCondition = null;
        user.Hobby = null;
        user.AdditionalInfo = null;
        user.Luggages.Clear();
        user.SpecialCards.Clear();
    }
    db.Games.Remove(game);
    db.SaveChanges();
    Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"Гру умпішно зупинено",
                                 cancellationToken: cancellationToken);
}
async void PauseGame(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = GetGame(chatId);
    if (game == null)
        return;
    if (game.IsPaused)
    {
        Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"Гра вже на паузі",
                                 cancellationToken: cancellationToken);
    }
    else
    {
        game.IsPaused = true;
        Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"Гру поставлено на паузу",
                                 cancellationToken: cancellationToken);
    }
    db.SaveChanges();

}
async void UnpauseGame(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = GetGame(chatId);
    if (game == null)
        return;
    if (!game.IsPaused)
    {
        Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"Гра не на паузі",
                                 cancellationToken: cancellationToken);
    }
    else
    {
        game.IsPaused = false;
        Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"Гру прибрано з паузи",
                                 cancellationToken: cancellationToken);
    }
    db.SaveChanges();

}
async void StartGame(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = GetGame(chatId);
    if (game == null)
    {
        Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"В даному чаті створену гру не знайдено",
                                 cancellationToken: cancellationToken);
        return;
    }
    GiveUserStats(game.Id);
    GiveGameStats(game.Id);
    db.SaveChanges();
    SendStatsToAll(game.Id, botClient);
}
void GiveUserStats(int gameId)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    List<Profession> listProffesions = new(db.Professions);
    List<Hobby> listHobbies = new(db.Hobbies);
    List<Luggage> listLuggages = new(db.Luggages);
    List<HealthCondition> listHealthConditions = new(db.HealthConditions);
    List<Biology> listBiologies = new(db.Biologies);
    List<AdditionalInfo> listAdditionalInfos = new(db.AdditionalInfo);
    List<SpecialCard> listSpecialCard = new(db.SpecialCards);
    foreach (BUser user in game.Users)
    {
        int i = Random.Shared.Next(0, listProffesions.Count);
        user.Profession = (listProffesions[i]);
        listProffesions.RemoveAt(i);
        i = Random.Shared.Next(0, listLuggages.Count);
        user.Luggages.Add(listLuggages[i]);
        listLuggages.RemoveAt(i);
        i = Random.Shared.Next(0, listHealthConditions.Count);
        user.HealthCondition = (listHealthConditions[i]);
        listHealthConditions.RemoveAt(i);
        i = Random.Shared.Next(0, listHobbies.Count);
        user.Hobby = (listHobbies[i]);
        listHobbies.RemoveAt(i);
        i = Random.Shared.Next(0, listSpecialCard.Count);
        user.SpecialCards[0] = (listSpecialCard[i]);
        listSpecialCard.RemoveAt(i);
        i = Random.Shared.Next(0, listBiologies.Count);
        user.Biology = (listBiologies[i]);
        listBiologies.RemoveAt(i);
        i = Random.Shared.Next(0, listAdditionalInfos.Count);
        user.AdditionalInfo = (listAdditionalInfos[i]);
        listAdditionalInfos.RemoveAt(i);
    }
}
void GiveGameStats(int gameId)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    List<Catastrophe> catastrophes = new(db.Catastrophes);
    List<Hazard> hazards = new(db.Hazards);
    List<BunkerInfo> bunkerInfos = new(db.BunkerInfos);
    int i = Random.Shared.Next(0, catastrophes.Count);
    game.Catastrophe = catastrophes[i];
    for (int j = 0; j < game.Users.Count; j++)
    {
        i = Random.Shared.Next(0, hazards.Count);
        game.Hazards.Add(hazards[i]);
        hazards.RemoveAt(i);
        i = Random.Shared.Next(0, bunkerInfos.Count);
        game.BunkerInfos.Add(bunkerInfos[i]);
        bunkerInfos.RemoveAt(i);
    }

}

void SendStatsToAll(int gameId, ITelegramBotClient botClient)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    foreach (BUser user in game.Users)
    {
        MainMenu(user, botClient);
    }
}
async void MainMenu(BUser user, ITelegramBotClient botClient)
{

    string ProfMarker = ":full_moon_with_face:";
    string BioMarker = ":full_moon_with_face:";
    string HealthMarker = ":full_moon_with_face:";
    string HobbyMarker = ":full_moon_with_face:";
    string LugMarker = ":full_moon_with_face:";
    string InfoMarker = ":full_moon_with_face:";
    string FCardMarker = ":full_moon_with_face:";
    string SCardMarker = ":full_moon_with_face:";
    List<InlineKeyboardButton> keyboardButtons = new();
    if (!user.ProfessionOpened)
    {
        ProfMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити професію", $"'{user.Id}'_openProffession"));
    }
    if (!user.BiologyOpened)
    {

        BioMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити біологію", $"'{user.Id}'_openBiology"));
    }
    if (!user.HealthConditionOpened)
    {
        HealthMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити стан здоров'я", $"'{user.Id}'_openHealth"));
    }
    if (!user.HobbyOpened)
    {
        HobbyMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити хоббі", $"'{user.Id}'_openHobby"));
    }
    if (!user.LuggagesOpened)
    {
        LugMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити багаж", $"'{user.Id}'_openLuggage"));
    }
    if (!user.AdditionalInfoOpened)
    {
        InfoMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити факт", $"'{user.Id}'_openAddInfo"));
    }
    if (!user.FirstSpecialCardUsed)
    {
        FCardMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити спеціальну карту", $"'{user.Id}'_openFirstSCard"));
    }
    if (user.SpecialCards.Count > 1 && !user.SecondSpecialCardUsed)
    {
        SCardMarker = ":new_moon_with_face:";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити ДОДАТКОВУ спеціальну карту", $"'{user.Id}'_openSecondSCard"));
    }
    if (user.BGame.Admin == user)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Меню Адміністратора", $"'{user.Id}'_sendAdminMenu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Персонажі гравців", $"'{user.Id}'_sendCharactersMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    long chatId = user.TelegramId;
    string Lugtext = string.Join(" ", user.Luggages.Select(d => d.Name));
    string text = $"Ваші характеристики:\n " +
                  $"'{ProfMarker}'|Професія: '{user.Profession!.Name}'\n" +
                  $"'{BioMarker}'|Біологія: '{user.Biology!.Name}'\n" +
                  $"'{HealthMarker}'|Стан здоров'я: '{user.HealthCondition!.Name}'\n" +
                  $"'{HobbyMarker}'|Хоббі:'{user.Hobby!.Name}'\n" +
                  $"'{LugMarker}'|Багаж: '{user.Luggages[0].Name}'\n" +
                  $"'{InfoMarker}'|Факт: '{user.AdditionalInfo!.Name}'\n" +
                  $"'{FCardMarker}'|Спеціальна карта: '{user.SpecialCards[0]!.Name}'\n";
    if (user.SpecialCards.Count > 1)
    {
        text += $"'{SCardMarker}'|ДОДАТКОВА спеціальна карта: '{user.SpecialCards[1]!.Name}'";
    }
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: text);
    user.MenuMessageId = sentMessage.MessageId;
    db.SaveChanges();

}

async void CharactersMenu(BUser user, ITelegramBotClient botClient)
{
    List<InlineKeyboardButton> keyboardButtons = new();
}

BUser? GetUser(long userChatId)
{
    return db.Users.FirstOrDefault(d => d.TelegramId == userChatId);
}
BGame? GetGame(long chatId)
{
    return db.Games.FirstOrDefault(d => d.GroupId == chatId);
}


async void OpenStat(BUser user, string type, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    switch (type)
    {
        case "Prof":
            {
                user.ProfessionOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою професію: '{user.Profession.Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);

            }
            break;
        case "Bio":
            {
                user.BiologyOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою біологію: '{user.Biology.Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
            }
            break;
        case "Hob":
            {
                user.HobbyOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває своє хоббі: '{user.Hobby.Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
            }
            break;
        case "Heal":
            {
                user.HealthConditionOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свій стан здоров'я: '{user.HealthCondition.Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
            }
            break;
        case "Info":
            {
                user.AdditionalInfoOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває факт про себе: '{user.AdditionalInfo.Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
            }
            break;
        case "Lug":
            {
                user.LuggagesOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває своій багаж: '{user.Luggages[0].Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
            }
            break;
        case "Card1":
            {
                user.FirstSpecialCardUsed = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою спеціальну карту: '{user.SpecialCards[0].Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
                PauseGame(user.BGame.GroupId, botClient, cancellationToken);
            }
            break;
        case "Card2":
            {
                user.SecondSpecialCardUsed = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою ДОДАТКОВУ спеціальну карту: '{user.SpecialCards[1].Name}'",
                cancellationToken: cancellationToken);
                await botClient.DeleteMessageAsync(user.TelegramId, user.MenuMessageId);
                MainMenu(user, botClient);
                PauseGame(user.BGame.GroupId, botClient, cancellationToken);
            }
            break;

    }
}

void GiveNewCard(long userId, string type)
{
    List<Profession> listProffesions = new(db.Professions);
    List<Hobby> listHobbies = new(db.Hobbies);
    List<Luggage> listLuggages = new(db.Luggages);
    List<HealthCondition> listHealthConditions = new(db.HealthConditions);
    List<Biology> listBiologies = new(db.Biologies);
    List<AdditionalInfo> listAdditionalInfos = new(db.AdditionalInfo);
    List<SpecialCard> listSpecialCards = new(db.SpecialCards);
    BUser? user = db.Users.Find(userId);
    if (user == null)
        return;


    switch (type)
    {
        case "Prof":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listProffesions.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        if (u.Profession == listProffesions[i])
                        {
                            uni = false;
                        }
                    }
                }
                user.Profession = listProffesions[i];

            }
            break;
        case "Bio":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listBiologies.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        if (u.Biology == listBiologies[i])
                        {
                            uni = false;
                        }
                    }
                }
                user.Biology = listBiologies[i];
            }
            break;
        case "Hob":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listHobbies.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        if (u.Hobby == listHobbies[i])
                        {
                            uni = false;
                        }
                    }
                }
                user.Hobby = listHobbies[i];
            }
            break;
        case "Heal":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listHealthConditions.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        if (u.HealthCondition == listHealthConditions[i])
                        {
                            uni = false;
                        }
                    }
                }
                user.HealthCondition = listHealthConditions[i];
            }
            break;
        case "Info":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listAdditionalInfos.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        if (u.AdditionalInfo == listAdditionalInfos[i])
                        {
                            uni = false;
                        }
                    }
                }
                user.AdditionalInfo = listAdditionalInfos[i];
            }
            break;
        case "Lug":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listLuggages.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        foreach (Luggage l in u.Luggages)
                        {
                            if (l == listLuggages[i])
                            {
                                uni = false;
                            }
                        }
                    }
                }
                user.Luggages[0] = listLuggages[i];
            }
            break;
        case "Card":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listSpecialCards.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        foreach (SpecialCard l in u.SpecialCards)
                        {
                            if (l == listSpecialCards[i])
                            {
                                uni = false;
                            }
                        }
                    }
                }
                user.SpecialCards.Add(listSpecialCards[i]);
            }
            break;
    }
    db.SaveChanges();

}
void SwapCards(long user1Id, long user2Id, string type)
{
    BUser? user1 = db.Users.Find(user1Id);
    BUser? user2 = db.Users.Find(user2Id);
    if (user1 != null || user2 == null)
        return;
    switch (type)
    {
        case "Bio":
            {
                (user2.Biology, user1.Biology) = (user1.Biology, user2.Biology);
            }
            break;
        case "Hob":
            {
                (user2.Hobby, user1.Hobby) = (user1.Hobby, user2.Hobby);
            }
            break;
        case "Heal":
            {
                (user2.HealthCondition, user1.HealthCondition) = (user1.HealthCondition, user2.HealthCondition);
            }
            break;
        case "Info":
            {
                (user2.AdditionalInfo, user1.AdditionalInfo) = (user1.AdditionalInfo, user2.AdditionalInfo);
            }
            break;
        case "Lug":
            {
                (user2.Luggages, user1.Luggages) = (user1.Luggages, user2.Luggages);
            }
            break;
    }
    db.SaveChanges();
}
void SwapAllUsersCards(long gameId, string type)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    switch (type)
    {
        case "Bio":
            {
                List<Biology> listBio = new();
                List<BUser> listBusers = new();
                foreach (BUser u in game.Users)
                {
                    if (u.BiologyOpened)
                    {
                        listBio.Add(u.Biology);
                        listBusers.Add(u);
                    }
                }
                int i;
                foreach (BUser u in listBusers)
                {
                    i = Random.Shared.Next(0, listBio.Count);
                    u.Biology = (listBio[i]);
                    listBio.RemoveAt(i);
                }
            }
            break;
        case "Hob":
            {
                List<Hobby> listHob = new();
                List<BUser> listBusers = new();
                foreach (BUser u in game.Users)
                {
                    if (u.HobbyOpened)
                    {
                        listHob.Add(u.Hobby);
                        listBusers.Add(u);
                    }
                }
                int i;
                foreach (BUser u in listBusers)
                {
                    i = Random.Shared.Next(0, listHob.Count);
                    u.Hobby = (listHob[i]);
                    listHob.RemoveAt(i);
                }
            }
            break;
        case "Heal":
            {
                List<HealthCondition> listHealth = new();
                List<BUser> listBusers = new();
                foreach (BUser u in game.Users)
                {
                    if (u.HealthConditionOpened)
                    {
                        listHealth.Add(u.HealthCondition);
                        listBusers.Add(u);
                    }
                }
                int i;
                foreach (BUser u in listBusers)
                {
                    i = Random.Shared.Next(0, listHealth.Count);
                    u.HealthCondition = (listHealth[i]);
                    listHealth.RemoveAt(i);
                }
            }
            break;
        case "Info":
            {
                List<AdditionalInfo> listInfo = new();
                List<BUser> listBusers = new();
                foreach (BUser u in game.Users)
                {
                    if (u.AdditionalInfoOpened)
                    {
                        listInfo.Add(u.AdditionalInfo);
                        listBusers.Add(u);
                    }
                }
                int i;
                foreach (BUser u in listBusers)
                {
                    i = Random.Shared.Next(0, listInfo.Count);
                    u.AdditionalInfo = (listInfo[i]);
                    listInfo.RemoveAt(i);
                }
            }
            break;
        case "Lug":
            {
                int twoLugUser = -1;
                List<Luggage> listLug = new();
                List<BUser> listBusers = new();
                foreach (BUser u in game.Users)
                {
                    if (u.LuggagesOpened && u.Luggages.Count > 1)
                    {
                        listLug.Add(u.Luggages[0]);
                        listLug.Add(u.Luggages[1]);
                        twoLugUser = listBusers.Count;
                        listBusers.Add(u);
                    }
                    else if (u.LuggagesOpened && u.Luggages.Count == 1)
                    {
                        listLug.Add(u.Luggages[0]);
                        listBusers.Add(u);
                    }
                    else if (!u.LuggagesOpened && u.Luggages.Count > 1)
                    {
                        listLug.Add(u.Luggages[1]);
                        twoLugUser = listBusers.Count;
                        listBusers.Add(u);
                    }
                }
                int i;
                for (int j = 0; j < listBusers.Count; j++)
                {
                    BUser u = listBusers[j];
                    if (j == twoLugUser && listBusers[j].LuggagesOpened)
                    {
                        for (int d = 0; d < 2; d++)
                        {
                            i = Random.Shared.Next(0, listLug.Count);
                            u.Luggages[d] = listLug[i];
                            listLug.RemoveAt(i);
                        }

                    }
                    else if (j == twoLugUser && !listBusers[j].LuggagesOpened)
                    {
                        i = Random.Shared.Next(0, listLug.Count);
                        u.Luggages[1] = listLug[i];
                        listLug.RemoveAt(i);
                    }
                    else
                    {
                        i = Random.Shared.Next(0, listLug.Count);
                        u.Luggages[0] = listLug[i];
                        listLug.RemoveAt(i);
                    }
                }

            }
            break;
    }
    db.SaveChanges();

}

void StealLuggage(long user1Id, long user2Id)
{
    BUser user1 = GetUser(user1Id);
    BUser user2 = GetUser(user2Id);
    user1.Luggages.Add(user2.Luggages[0]);
    user2.Luggages.Clear();
    db.SaveChanges();
}

async void SendAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Почати голосування", $"'{user.Id}'_startVoting"),
                        InlineKeyboardButton.WithCallbackData("ПАУЗА", $"'{user.Id}'_pauseGame"),
                        InlineKeyboardButton.WithCallbackData("ПРИБРАТИ З ПАУЗИ", $"'{user.Id}'_unpauseGame"),
                        InlineKeyboardButton.WithCallbackData("Змінити Характеристику", $"'{user.Id}'_ChangeCardMenu"),
                        InlineKeyboardButton.WithCallbackData("Обміняти Характеристики", $"'{user.Id}'_SwapCardsMenu"),
                        InlineKeyboardButton.WithCallbackData("Перемішати серед усіх Характеристики", $"'{user.Id}'_ShuffleCardsMenu"),
                        InlineKeyboardButton.WithCallbackData("Вкрасти багаж", $"'{user.Id}'_StealLuggageMenu"),
                        InlineKeyboardButton.WithCallbackData("Змінити Характеристику Бункера", $"'{user.Id}'_BunkerCardMenu"),
                        InlineKeyboardButton.WithCallbackData("Передати роль ведучого", $"'{user.Id}'_NewAdminMenu"),
                        InlineKeyboardButton.WithCallbackData("Редагувати голоси",$"'{user.Id}'_VotesMenu"),
                        InlineKeyboardButton.WithCallbackData("ГОЛОВНЕ МЕНЮ", $"'{user.Id}'_MainMenu")
                    });
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: "Меню Ведучого");
    user.MenuMessageId = sentMessage.MessageId;
}

async void SendNewCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'_ChangeCard2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: "Оберіть гравця");
    user.MenuMessageId = sentMessage.MessageId;
}
async void SendNewCard2AdminMenu(BUser admin, BUser target, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    await botClient.DeleteMessageAsync(chatId, admin.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    if (target.ProfessionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Професія '{target.Profession.Name}'", $"'{target.Id}'_changeProfession"));
    }
    if (target.BiologyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Біологія '{target.Biology.Name}'", $"'{target.Id}'_changeBiology"));
    }
    if (target.HealthConditionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Стан здоров'я '{target.HealthCondition.Name}'", $"'{target.Id}'_changeHealth"));
    }
    if (target.HobbyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Хоббі '{target.Hobby.Name}'", $"'{target.Id}'_changeHobby"));
    }
    if (target.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Багаж '{target.Luggages[0].Name}'", $"'{target.Id}'_changeFirstLuggage"));
    }
    if (target.Luggages.Count > 1)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Додатковий Багаж '{target.Luggages[1].Name}'", $"'{target.Id}'_changeSecondLuggage"));
    }
    if (target.AdditionalInfoOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Факт '{target.AdditionalInfo.Name}'", $"'{target.Id}'_changeAddInfo"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору гравця", $"'{admin.Id}'_ChangeCardMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: $"'{target.Name}'|Оберіть характеристику");
    admin.MenuMessageId = sentMessage.MessageId;
}

async void SwapCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'_SwapCard2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: "Оберіть першого гравця");
    user.MenuMessageId = sentMessage.MessageId;
}
async void SwapCard2AdminMenu(BUser admin, BUser target1, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    await botClient.DeleteMessageAsync(chatId, admin.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    foreach (BUser u in admin.BGame.Users)
    {
        if (u != target1)
        {
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{admin.Id}'.'{u.Id}'.'{target1.Id}'_SwapCard3Menu"));
        }
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору 1 гравця", $"'{admin.Id}'_SwapCardsMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: "Оберіть другого гравця");
    admin.MenuMessageId = sentMessage.MessageId;
}
async void SwapCard3AdminMenu(BUser admin, BUser target1, BUser target2, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    await botClient.DeleteMessageAsync(chatId, admin.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    if (target1.ProfessionOpened && target2.ProfessionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Професія ", $"'{target1.Id}'.'{target2.Id}'_swapProfession"));
    }
    if (target1.BiologyOpened && target2.BiologyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Біологія ", $"'{target1.Id}'.'{target2.Id}'_swapBiology"));
    }
    if (target1.HealthConditionOpened && target2.HealthConditionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Стан здоров'я ", $"'{target1.Id}'.'{target2.Id}'_swapHealth"));
    }
    if (target1.HobbyOpened && target2.HobbyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Хоббі ", $"'{target1.Id}'.'{target2.Id}'_swapHobby"));
    }
    if (target1.LuggagesOpened && target2.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Основний багаж '{target1.Name}' та основний багаж '{target2.Name}' ", $"'{target1.Id}'.'{target2.Id}'_swap11Luggage"));
    }
    if (target1.LuggagesOpened && target2.Luggages.Count > 1)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Основний багаж '{target1.Name}' та додатковий багаж '{target2.Name}' ", $"'{target1.Id}'.'{target2.Id}'_swap12Luggage"));
    }
    if (target1.Luggages.Count > 1 && target2.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Додатковий багаж '{target1.Name}' та основний багаж '{target2.Name}' ", $"'{target1.Id}'.'{target2.Id}'_swap21Luggage"));
    }
    if (target1.AdditionalInfoOpened && target2.AdditionalInfoOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Факт ", $"'{target1.Id}'.'{target2.Id}'_swapAddInfo"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору гравців", $"'{admin.Id}'_SwapCardsMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: $"'{target1.Name}','{target2.Name}'|Оберіть характеристику");
    admin.MenuMessageId = sentMessage.MessageId;
}

async void ShuffleCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті професії", $"'{user.Id}'_shuffleProfession"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Вся відкрита біологія", $"'{user.Id}'_shuffleBiology"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті стани здоров'я", $"'{user.Id}'_shuffleHealth"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті хоббі ", $"'{user.Id}'_shuffleHobby"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті багажі", $"'{user.Id}'_shuffleLuggage"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті факти ", $"'{user.Id}'_shuffleAddInfo"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: $"Оберіть характеристику");
    user.MenuMessageId = sentMessage.MessageId;
}

async void StealLuggageAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'_StealLuggage2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: "Оберіть гравця, що краде багаж");
    user.MenuMessageId = sentMessage.MessageId;
}
async void StealLuggage2AdminMenu(BUser admin, BUser stealer, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    await botClient.DeleteMessageAsync(chatId, admin.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    foreach (BUser u in admin.BGame.Users)
    {
        if (u != stealer)
        {
            if (u.LuggagesOpened)
            {
                keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Luggages[0].Name}' у '{u.Name}'", $"'{stealer.Id}'.'{u.Id}'_stealLuggage"));
            }
        }
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору крадія", $"'{admin.Id}'_StealLuggageMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: $"'{stealer.Name}'| Оберіть ціль крадіжки");
    admin.MenuMessageId = sentMessage.MessageId;
}

async void BunkerCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    for (int i = 0; i < user.BGame.Status; i++)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{user.BGame.BunkerInfos[i].Name}'", $"'{user.Id}'.'{i}'_BunkerCard2Menu"));
    }
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: $"Оберіть характеристику бункера");
    user.MenuMessageId = sentMessage.MessageId;
}
async void BunkerCard2AdminMenu(BUser user, int cardNumber, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    await botClient.DeleteMessageAsync(chatId, user.MenuMessageId, cancellationToken);
    List<InlineKeyboardButton> keyboardButtons = new();
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Замінити на нову", $"'{user.Id}'.'{cardNumber}_newBunkerCard"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Скинути", $"'{user.Id}'.'{cardNumber}_removeBunkerCard"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup: inlineKeyboard,
                  text: $"\"'{user.BGame.BunkerInfos[cardNumber].Name}'\"\n Що зробити з картою?");
    user.MenuMessageId = sentMessage.MessageId;
}
