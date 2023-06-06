﻿using BunkerBot;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var db = new AppDbContext();
db.Database.EnsureCreated();

Regex regex = new(@"(.+)_(.+)", RegexOptions.Compiled);
Regex chatIdRegex = new(@"/start chatId=(.+)", RegexOptions.Compiled);

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
    cancellationToken: cts.Token);
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
            var m = chatIdRegex.Match(message.Text);
            var chatId = message.Chat.Id;
            if (m.Success)
            {
                long gameChatId;
                try
                {
                    gameChatId = long.Parse(m.Groups[1].Value);
                }
                catch
                {
                    return;
                }
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
                case "/creategame":
                    {
                        CreateGame(chatId, botClient, cancellationToken, message);
                    }
                    break;
                case "/startgame":
                    {
                        BGame game = GetGame(chatId);
                        if (game != null)
                        {
                            if (game.Admin != null)
                            {
                                if (message.From.Id != game.Admin.TelegramId)
                                {
                                    Message sMessage = await botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: $"Почати гру може лише ведучий",
                                        cancellationToken: cancellationToken);
                                    break;
                                }
                            }
                            else
                            {
                                Message sMessage = await botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: $"Почати гру може лише ведучий",
                                        cancellationToken: cancellationToken);
                                break;
                            }

                        }
                        StartGame(chatId, botClient, cancellationToken);
                    }
                    break;
                default:
                    {
                        BGame game = GetGame(chatId);
                        if (game == null)
                            break;
                        if (game.SpeakerId == 0)
                            break;
                        if (game.SpeakerId == message.From.Id)
                            break;
                        else
                        {

                            await botClient.DeleteMessageAsync(chatId,message.MessageId, cancellationToken);
                            Message sMessage = await botClient.SendTextMessageAsync(
                                        chatId: message.From.Id,
                                        text: $"Зараз час промови іншого гравця. Почекайте будь-ласка зачекайте або загальний час для розмови",
                                        cancellationToken: cancellationToken);
                        }
                     

                    }
                    
                    break;
                case "/stopgame":
                    {
                        BGame game = GetGame(chatId);
                        if (game != null)
                        {
                            if (game.Admin != null)
                            {
                                if (message.From.Id != game.Admin.TelegramId)
                                {
                                    Message sMessage = await botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: $"Завершити гру може лише ведучий",
                                        cancellationToken: cancellationToken);
                                    return;
                                }
                            }
                            else
                            {
                                Message sMessage = await botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: $"Завершити гру може лише ведучий",
                                        cancellationToken: cancellationToken);
                                return;
                            }
                            
                        }
                        StopGame(chatId, botClient, cancellationToken);
                    }
                    break;
            }



            db.SaveChanges();

        }
    }
    if (update.Type == UpdateType.CallbackQuery)
    {
        var callback = update.CallbackQuery;
        var chatId = callback.Message.Chat.Id;
        var c = chatIdRegex.Match(callback.Data);
        if (c.Groups.Count == 3)
        {
            var idData = c.Groups[1].Value;
            var command = c.Groups[2].Value;
            switch (command)
            {
                case "startHazards":
                    {
                        BGame game = db.Games.Find(idData);
                        if (game.Status > 6)
                            break;
                        game.Status = 7;
                        db.SaveChanges();
                        StartHazards( int.Parse(idData), botClient, cancellationToken);
                    }
                    break;
                case "winHazard":
                    {
                        WinHazard(int.Parse(idData), botClient, cancellationToken);
                    }
                    break;
                case "loseHazard":
                    {
                        LoseHazard(int.Parse(idData), botClient, cancellationToken);
                    }
                    break;
                case "openProfession":
                    {
                        BUser user=db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "Profession", botClient, cancellationToken);
                    }
                    break;
                case "openBiology":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "Biology", botClient, cancellationToken);
                    }
                    break;
                case "openHobby":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "Hobby", botClient, cancellationToken);
                    }
                    break;
                case "openHealth":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "Health", botClient, cancellationToken);
                    }
                    break;
                case "openLuggage":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "Luggage", botClient, cancellationToken);
                    }
                    break;
                case "openAddInfo":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "AddInfo", botClient, cancellationToken);
                    }
                    break;
                case "openFirstSCard":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "FirstSCard", botClient, cancellationToken);
                    }
                    break;
                case "openSecondSCard":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        OpenStat(user, "SecondSCard", botClient, cancellationToken);
                    }
                    break;
                case "AdminMenu":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        AdminMenu(user,botClient,cancellationToken);
                    }
                    break;
                case "CharactersMenu":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        CharactersMenu(user,0, botClient, cancellationToken);
                    }
                    break;
                case "GameInfoMenu":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        GameInfoMenu(user, botClient, cancellationToken);
                    }
                    break;
                case "vote":
                    {
                        var data=idData.Split('.');
                        BUser user = db.Users.Find(data[0]);
                        if (user == null)
                            break;
                        BUser user2 = db.Users.Find(data[1]);
                        if (user == null)
                            break;
                        user.VotedFor=user2;
                        db.SaveChanges();
                        await botClient.DeleteMessageAsync(user.TelegramId, user.VoteMessageId, cancellationToken);
                        Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: user.BGame.GroupId,
                                 text: $"'{user.Name} проголосував'",
                                 cancellationToken: cancellationToken);

                    }
                    break;
                case "voteOut1":
                    {
                        BUser user = db.Users.Find(idData);
                        if (user == null)
                            break;
                        user.IsVotedOut = true;
                        Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: user.BGame.GroupId,
                                 text: $"'{user.Name} тепер вигнанець'",
                                 cancellationToken: cancellationToken);
                        if (user.BGame.VotingList.roundVotings[user.BGame.Status] == 2)
                        {
                            Message s2Message = await botClient.SendTextMessageAsync(
                                 chatId: user.BGame.GroupId,
                                 text: $"'{user.Name} тепер вигнанець'",
                                 cancellationToken: cancellationToken);
                        }
                    }
                    break;
                case "":
                    {

                    }
                    break;

            }
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
            UpdateGameMembersList(game.GroupId);
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
    UpdateGameMembersList(game.GroupId);
    db.SaveChanges();
    return true;
}

async void UpdateGameMembersList(long chatId)
{
    BGame game = GetGame(chatId);
    if (game == null)
        return;
    string text = "Приєдналися:\n";
    for (int i = 0; i < game.Users.Count - 1; i++)
    {
        text += $"'{game.Users[i].Name}', ";
    }
    text += $"'{game.Users.Last().Name}'";

    Message message = await botClient.EditMessageTextAsync(chatId, game.StartGameBotMessageId, text);
}
async Task<BUser> AddUser(long userId)
{
    BUser user = new BUser();
    user.TelegramId = userId;
    var chat = await botClient.GetChatAsync(userId);
    user.Name = $"'{chat.FirstName}' '{chat.LastName}'";
    db.Users.Add(user);
    db.SaveChanges();
    return user;
}
async void CreateGame(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken, Message message)
{
    BGame existingGame = GetGame(chatId);
    if (existingGame != null)
    {
        Message sentMessage = await botClient.SendTextMessageAsync(
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
    Message sent1Message = await botClient.SendTextMessageAsync(
              chatId: chatId,
              text: $"'{message.From.FirstName}' '{message.From.LastName}' створив гру",
              replyMarkup: inlineKeyboard,
              cancellationToken: cancellationToken);
    Message sent2Message = await botClient.SendTextMessageAsync(
              chatId: chatId,
              text: $"Приєдналися:",
              cancellationToken: cancellationToken);
    game.StartGameBotMessageId = sent2Message.MessageId;
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
        user.IsVoteDoubled = false;
        user.IsVotedOut= false;
        user.IsDead= false;
        user.MenuMessageId = 0;
        user.VoteMessageId = 0;
        user.SpeakingButtonMessageId= 0;
        user.VotedFor = null;
        user.AsignedHazard = null;
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
                                 chatId: game.Admin.TelegramId,
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
    SendStatsToAll(game.Id, botClient, cancellationToken);

}
void GiveUserStats(int gameId)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    List<Profession> listProfesions = new(db.Professions);
    List<Hobby> listHobbies = new(db.Hobbies);
    List<Luggage> listLuggages = new(db.Luggages);
    List<HealthCondition> listHealthConditions = new(db.HealthConditions);
    List<Biology> listBiologies = new(db.Biologies);
    List<AdditionalInfo> listAdditionalInfos = new(db.AdditionalInfo);
    List<SpecialCard> listSpecialCard = new(db.SpecialCards);
    foreach (BUser user in game.Users)
    {
        int i = Random.Shared.Next(0, listProfesions.Count);
        user.Profession = (listProfesions[i]);
        listProfesions.RemoveAt(i);
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
    game.VotingList = db.VotingLists.Find(game.Users.Count);

}
async void NewRound(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    game.Status++;
    if (game.Status == 6)
    {
        string text = "Перший етап завершено! Гравці що потрапили в бункер:\n";
        foreach(BUser u in game.Users)
        {
            if (!u.IsVotedOut)
            {
                text += $"'{u.Name}'\n";
            }
        }
        game.SpeakerId= 0;
        text += "Гравці можуть розкрити всі свої характеристики(окрім спеціальних карт, їх ще можна буде використати). \nКоли всі будуть готові ведучий може почати раунд загроз і ви зможете дізнатися хто виживе, а хто ні";
        
        Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.Admin.TelegramId,
                                 text: $"Раунд Загроз",
                                 cancellationToken: cancellationToken);
        InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Почати", $"'{game.Id}'.'{sentMessage.MessageId}'_startHazards"),
                    });
        await botClient.EditMessageReplyMarkupAsync(game.Admin.TelegramId, sentMessage.MessageId, inlineKeyboard);
    }
    else
    {
        Message sMessage = await botClient.SendTextMessageAsync(
                             chatId: game.GroupId,
                             text: $"Починаєтся '{game.Status}' раунд\nБуде проведено '{game.VotingList.roundVotings[game.Status]}' голосувань",
                             cancellationToken: cancellationToken);
        Message s2Message = await botClient.SendTextMessageAsync(
                             chatId: game.GroupId,
                             text: $"Відкрито нову характеристику бункера:\n '{game.BunkerInfos[game.Status-1]}'",
                             cancellationToken: cancellationToken);
    }
    game.RoundPart = 1;
    db.SaveChanges();
    for (int j = 0; j < game.Users.Count; j++)
    {
        if (!game.Users[j].IsVotedOut)
        {
            GiveSpeakingTime(game.Users[j], botClient, cancellationToken);
            return;
        }
    }
}
async void StartHazards(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    AsignHazards(gameId);
    Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Починаєтся раунд загроз для Жителів бункера",
                                 cancellationToken: cancellationToken);
    GiveHazard(gameId,botClient,cancellationToken);
}
async void StartHazardsExile(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Починаєтся раунд загроз для вигнанців",
                                 cancellationToken: cancellationToken);
    GiveHazardExile(gameId, botClient, cancellationToken);
}
void GiveHazard(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    if (game.CurrentHazzardTargetId == 0)
    {
        foreach (BUser u in game.Users)
        {
            if (!u.IsVotedOut)
            {
                game.CurrentHazzardTargetId = u.TelegramId;
                db.SaveChanges();
                SendHazard(gameId, botClient, cancellationToken);
                return;
            }
        }

    }
    else
    {
        BUser user = GetUser(game.CurrentHazzardTargetId);
        if (user == null)
            return;
        if (game.Users.IndexOf(user) == game.Users.Count - 1)
        {
            game.CurrentHazzardTargetId = 0;
            db.SaveChanges();
            StartHazardsExile(gameId,botClient,cancellationToken);
        }
        else
        {
            for (int i = game.Users.IndexOf(user) + 1; i < game.Users.Count; i++)
            {
                if (!game.Users[i].IsVotedOut)
                {
                    game.CurrentHazzardTargetId = game.Users[i].TelegramId;
                    db.SaveChanges();
                    SendHazard(gameId, botClient, cancellationToken);
                    return;
                }
            }
            game.CurrentHazzardTargetId = 0;
            db.SaveChanges();
            StartHazardsExile(gameId, botClient, cancellationToken);
        }
    }
}

void GiveHazardExile(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    if (game.CurrentHazzardTargetId == 0)
    {
        foreach (BUser u in game.Users)
        {
            if (u.IsVotedOut)
            {
                game.CurrentHazzardTargetId = u.TelegramId;
                db.SaveChanges();
                SendHazardExile(gameId, botClient, cancellationToken);
                return;
            }
        }

    }
    else
    {
        BUser user = GetUser(game.CurrentHazzardTargetId);
        if (user == null)
            return;
        if (game.Users.IndexOf(user) == game.Users.Count - 1)
        {
            game.CurrentHazzardTargetId = 0;
            db.SaveChanges();
            EndGame(gameId,botClient,cancellationToken);
        }
        else
        {
            for (int i = game.Users.IndexOf(user) + 1; i < game.Users.Count; i++)
            {
                if (game.Users[i].IsVotedOut)
                {
                    game.CurrentHazzardTargetId = game.Users[i].TelegramId;
                    db.SaveChanges();
                    SendHazard(gameId, botClient, cancellationToken);
                    return;
                }
            }

        }
    }
}

async void EndGame(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    string text = "Кінець гри! Гравці, що вижили:\n";
    foreach(BUser u in game.Users)
    {
        if (!u.IsDead)
        {
            text += u.Name;
            if (u.IsVotedOut)
            {
                text+=" Вигнанець";
            }
            text+="\n";
        }
    }
    text += "Зараз можете обговорити результати і вирішити чи зможуть гравці, що вижили, продовжити людськи рід та перемогти катастрофу\n Щоб закінчити гру ведучий повинне ввести /stopgame";
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: text,
                                 cancellationToken: cancellationToken);
}
async void SendHazard(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    BUser user = GetUser(game.CurrentHazzardTargetId);
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Загроза гравця '{user.Name}'\n Ви можете використовувати все що є в бункері та живих персонажів що до нього потрапили та багаж мертвих жителів бункера для того щоб перемогти загрозу.\n У разі невдачі '{user.Name}' помре",
                                 cancellationToken: cancellationToken);
    Message s2Message = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Загроза:\n '{user.AsignedHazard.Name}'",
                                 cancellationToken: cancellationToken);
    
    Message sentAdminMessage = await botClient.SendTextMessageAsync(
          chatId: user.BGame.Admin.TelegramId,
          text: $"Чи перемагають гравці загрозу?",
          cancellationToken: cancellationToken);
    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Так", $"'{game.Id}'.'{sentAdminMessage.MessageId}'_winHazard"),
                        InlineKeyboardButton.WithCallbackData("Ні", $"'{game.Id}'.'{sentAdminMessage.MessageId}'_loseHazard")
                    });
    await botClient.EditMessageReplyMarkupAsync(user.BGame.Admin.TelegramId, sentAdminMessage.MessageId, inlineKeyboard);
}
async void SendHazardExile(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    BUser user = GetUser(game.CurrentHazzardTargetId);
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Загроза гравця '{user.Name}'\n Ви можете використовувати живих персонажів вигнанців та багаж мертвих вигнанців для того щоб перемогти загрозу.\n У разі невдачі '{user.Name}' помре",
                                 cancellationToken: cancellationToken);
    Message s2Message = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Загроза:\n '{user.AsignedHazard.Name}'",
                                 cancellationToken: cancellationToken);
    Message sentAdminMessage = await botClient.SendTextMessageAsync(
          chatId: user.BGame.Admin.TelegramId,
          text: $"Чи перемагають гравці загрозу?",
          cancellationToken: cancellationToken);
    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Так", $"'{game.Id}'.'{sentAdminMessage.MessageId}'_winHazardExile"),
                        InlineKeyboardButton.WithCallbackData("Ні", $"'{game.Id}'.'{sentAdminMessage.MessageId}'_loseHazardExile")
                    });
    await botClient.EditMessageReplyMarkupAsync(user.BGame.Admin.TelegramId, sentAdminMessage.MessageId, inlineKeyboard);
}
async void LoseHazard(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    BUser user = GetUser(game.CurrentHazzardTargetId);
    user.IsDead = true;
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Ви не впоралися з загрозою. '{user.Name}' помирає\n Ви більше не можете використовувати можливості його персонажа(окрім багажу) для боротьби з наступними загрозами",
                                 cancellationToken: cancellationToken);
    GiveHazard(gameId, botClient, cancellationToken);
    db.SaveChanges();
}
async void WinHazard(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Ви перемогли загрозу!",
                                 cancellationToken: cancellationToken);
    GiveHazard(gameId, botClient, cancellationToken);
}

async void LoseHazardExile(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    BUser user = GetUser(game.CurrentHazzardTargetId);
    user.IsDead = true;
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Ви не впоралися з загрозою. '{user.Name}' помирає\n Ви більше не можете використовувати можливості його персонажа(окрім багажу) для боротьби з наступними загрозами",
                                 cancellationToken: cancellationToken);
    GiveHazardExile(gameId, botClient, cancellationToken);
    db.SaveChanges();
}
async void WinHazardExile(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Ви перемогли загрозу!",
                                 cancellationToken: cancellationToken);
    GiveHazardExile(gameId, botClient, cancellationToken);
}
async void AsignHazards(int gameId)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    for (int i = 0; i < game.Users.Count; i++)
    {
        game.Users[i].AsignedHazard = game.Hazards[i];
    }
    db.SaveChanges();
}
async void SendStatsToAll(int gameId, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    foreach (BUser user in game.Users)
    {
        MainMenu(user, botClient, cancellationToken);
    }
    Message sMessage = await botClient.SendTextMessageAsync(
                                 chatId: game.GroupId,
                                 text: $"Гра починаєтся\n Катастрофа:\n '{game.Catastrophe.Name}'",
                                 cancellationToken: cancellationToken);

}
async void MainMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    string ProfMarker = "%F0%9F%94%92";
    string BioMarker = "%F0%9F%94%92";
    string HealthMarker = "%F0%9F%94%92";
    string HobbyMarker = "%F0%9F%94%92";
    string LugMarker = "%F0%9F%94%92";
    string InfoMarker = "%F0%9F%94%92";
    string FCardMarker = "%F0%9F%94%92";
    string SCardMarker = "%F0%9F%94%92";
    List<InlineKeyboardButton> keyboardButtons = new();
    if (!user.ProfessionOpened && (user.BGame.SpeakerId == user.TelegramId || user.BGame.Status==6))
    {
        ProfMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити професію", $"'{user.Id}'_openProfession"));
    }
    if (!user.BiologyOpened && (user.BGame.SpeakerId == user.TelegramId || user.BGame.Status == 6))
    {

        BioMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити біологію", $"'{user.Id}'_openBiology"));
    }
    if (!user.HealthConditionOpened && (user.BGame.SpeakerId == user.TelegramId || user.BGame.Status == 6))
    {
        HealthMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити стан здоров'я", $"'{user.Id}'_openHealth"));
    }
    if (!user.HobbyOpened && (user.BGame.SpeakerId == user.TelegramId || user.BGame.Status == 6))
    {
        HobbyMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити хоббі", $"'{user.Id}'_openHobby"));
    }
    if (!user.LuggagesOpened && (user.BGame.SpeakerId == user.TelegramId || user.BGame.Status == 6))
    {
        LugMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити багаж", $"'{user.Id}'_openLuggage"));
    }
    if (!user.AdditionalInfoOpened && (user.BGame.SpeakerId == user.TelegramId || user.BGame.Status == 6))
    {
        InfoMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити факт", $"'{user.Id}'_openAddInfo"));
    }
    if (!user.FirstSpecialCardUsed)
    {
        FCardMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити спеціальну карту", $"'{user.Id}'_openFirstSCard"));
    }
    if (user.SpecialCards.Count > 1 && !user.SecondSpecialCardUsed)
    {
        SCardMarker = "%F0%9F%94%93";
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Розкрити ДОДАТКОВУ спеціальну карту", $"'{user.Id}'_openSecondSCard"));
    }
    
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
                  text: text,
                  cancellationToken: cancellationToken);
    if (user.BGame.Admin == user)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Меню Адміністратора", $"'{user.Id}'.'{sentMessage.MessageId}'_AdminMenu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Персонажі гравців", $"'{user.Id}'.'{sentMessage.MessageId}'_CharactersMenu"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Інформація про бункер та катастрофу", $"'{user.Id}'.'{sentMessage.MessageId}'_GameInfoMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);

}
async void VoteMenu(BUser user, int votingCount, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця, проти якого голосуєте",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_vote'{votingCount}'"));
    }
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void VoteMenuMax(BUser user, int votingCount, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця, проти якого голосуєте",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_vote'{votingCount}'"));
    }
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void VoteResults(int gameId, int votingCount, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    var chatId = game.GroupId;
    string text = "";
    int maxCount = -1;
    List<BUser> max = new();
    int count = 0;
    foreach (BUser u in game.Users)
    {
        text += $"За гравця '{u.Name}' проголосували: \n";
        count = 0;
        foreach (BUser u2 in game.Users)
        {
            if (u2.VotedFor == u)
            {
                if (u2.IsVoteDoubled)
                {
                    count += 2;
                    text += "Подвійний голос|";
                }
                else
                {
                    count++;
                }
                text += $"'{u2.Name}'\n";
            }
        }
        text += $"Всього '{count}' голосів \n\n";
        if (count > maxCount)
        {
            maxCount = count;
            max.Clear();
            max.Add(u);
        }
        else if (count == maxCount)
        {
            max.Add(u);
        }
    }
    text += "Найбільше голосів за \n";
    foreach (BUser u in max)
    {
        text += $"'{u.Name}'\n";
    }
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: text,
                  cancellationToken: cancellationToken);

    if (max.Count == 1)
    {
        List<InlineKeyboardButton> keyboardButtons = new();
        Message sentAdminMessage = await botClient.SendTextMessageAsync(
                  chatId: game.Admin.TelegramId,
                  text: $"Вигнати '{max[0].Name}'?",
                  cancellationToken: cancellationToken);
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Підтвердити", $"'{max[0].Id}'.'{sentAdminMessage.MessageId}'_voteOut'{votingCount}'"));
        InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
        await botClient.EditMessageReplyMarkupAsync(game.Admin.TelegramId, sentAdminMessage.MessageId, inlineKeyboard);
    }
    if (max.Count > 1)
    {
        sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оскільки по голосам лідує більше одного гравця, зараз буде надано додатковий час на виправдання для них, після якого буде проведено повторне голосування.",
                  cancellationToken: cancellationToken);
        GiveSpeakingTimeMaxVotes(max[0], botClient, cancellationToken);

    }
}
async void VoteResultsMax(int gameId, int votingCount, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    var chatId = game.GroupId;
    string text = "";
    int maxCount = -1;
    List<BUser> max = new();
    int count = 0;
    foreach (BUser u in game.MaxVotesUsers)
    {
        text += $"За гравця '{u.Name}' проголосували: \n";
        count = 0;
        foreach (BUser u2 in game.Users)
        {
            if (u2.VotedFor == u)
            {
                if (u2.IsVoteDoubled)
                {
                    count += 2;
                    text += "Подвійний голос|";
                }
                else
                {
                    count++;
                }
                text += $"'{u2.Name}'\n";
            }
        }
        text += $"Всього '{count}' голосів \n\n";
        if (count > maxCount)
        {
            maxCount = count;
            max.Clear();
            max.Add(u);
        }
        else if (count == maxCount)
        {
            max.Add(u);
        }
    }
    text += "Найбільше голосів за \n";
    foreach (BUser u in max)
    {
        text += $"'{u.Name}'\n";
    }
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: text,
                  cancellationToken: cancellationToken);

    if (max.Count == 1)
    {
        List<InlineKeyboardButton> keyboardButtons = new();
        Message sentAdminMessage = await botClient.SendTextMessageAsync(
                  chatId: game.Admin.TelegramId,
                  text: $"Вигнати '{max[0].Name}'?",
                  cancellationToken: cancellationToken);
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Підтвердити", $"'{max[0].Id}'.'{sentAdminMessage.MessageId}'_voteOut'{votingCount}'"));
        InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
        await botClient.EditMessageReplyMarkupAsync(game.Admin.TelegramId, sentAdminMessage.MessageId, inlineKeyboard);
    }
    if (max.Count > 1)
    {
        sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оскільки по голосам лідує більше одного гравця, вигнанцем стане випадковий з них.",
                  cancellationToken: cancellationToken);
        GiveSpeakingTimeMaxVotes(max[0], botClient, cancellationToken);

    }
}
async void EndSpeaking(BGame game, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    for (int i = 0; i < game.Users.Count; i++)
    {
        if (game.Users[i].TelegramId == game.SpeakerId)
        {
            Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: game.GroupId,
                  text: $"Час промови '{game.Users[i].Name}' закінчився",
                  cancellationToken: cancellationToken);

            for (int j = i + 1; j < game.Users.Count; j++)
            {
                if (!game.Users[j].IsVotedOut)
                {
                    GiveSpeakingTime(game.Users[j], botClient, cancellationToken);
                    return;
                }
            }
            sentMessage = await botClient.SendTextMessageAsync(
                chatId: game.GroupId,
                text: $"Починається загальний час \n Ведучий може почати голосування коли гравці домовляться",
                cancellationToken: cancellationToken);
            game.RoundPart = 2;
            db.SaveChanges();
            return;
        }
    }
}
async void GiveSpeakingTime(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    Message sentGroupMessage = await botClient.SendTextMessageAsync(
          chatId: user.BGame.GroupId,
          text: $"Починається час промови '{user.Name}'",
          cancellationToken: cancellationToken);
    user.BGame.SpeakerId = user.TelegramId;
    Message sentUserMessage = await botClient.SendTextMessageAsync(
          chatId: user.TelegramId,
          text: "Починається твій час промови.",
          cancellationToken: cancellationToken);
    Message sentAdminMessage = await botClient.SendTextMessageAsync(
          chatId: user.BGame.Admin.TelegramId,
          text: $"На випадок якщо '{user.Name}' \"Затягне\" з промовою",
          cancellationToken: cancellationToken);
    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Завершити промову", $"'{user.Id}'.'{sentUserMessage.MessageId}'.'{sentAdminMessage.MessageId}'_endSpeaking"),
                    });
    await botClient.EditMessageReplyMarkupAsync(user.TelegramId, sentUserMessage.MessageId, inlineKeyboard);
    await botClient.EditMessageReplyMarkupAsync (user.BGame.Admin.TelegramId, sentAdminMessage.MessageId, inlineKeyboard);
    db.SaveChanges();
}
async void GiveSpeakingTimeMaxVotes(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    Message sentGroupMessage = await botClient.SendTextMessageAsync(
          chatId: user.BGame.GroupId,
          text: $"Починається час виправдовування '{user.Name}'",
          cancellationToken: cancellationToken);
    user.BGame.SpeakerId = user.TelegramId;
    
    Message sentUserMessage = await botClient.SendTextMessageAsync(
          chatId: user.TelegramId,
          text: "Починається твій час виправдовування.",
          cancellationToken: cancellationToken);

    Message sentAdminMessage = await botClient.SendTextMessageAsync(
          chatId: user.BGame.Admin.TelegramId,
          text: $"На випадок якщо '{user.Name}' \"Затягне\" з виправдовуванням",
          cancellationToken: cancellationToken);
    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Завершити виправдовування", $"'{user.Id}'.'{sentUserMessage.MessageId}'.'{sentAdminMessage.MessageId}'_endSpeakingMaxVotes"),
                    });
    await botClient.EditMessageReplyMarkupAsync(user.TelegramId, sentUserMessage.MessageId, inlineKeyboard);
    await botClient.EditMessageReplyMarkupAsync(user.BGame.Admin.TelegramId, sentAdminMessage.MessageId, inlineKeyboard);
    db.SaveChanges();
}
async void EndSpeakingMaxVotes(BGame game, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    for (int i = 0; i < game.MaxVotesUsers.Count; i++)
    {
        if (game.MaxVotesUsers[i].TelegramId == game.SpeakerId)
        {
            Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: game.GroupId,
                  text: $"Час промови '{game.MaxVotesUsers[i].Name}' закінчився",
                  cancellationToken: cancellationToken);
            if (i == game.MaxVotesUsers.Count - 1)
            {
                sentMessage = await botClient.SendTextMessageAsync(
                    chatId: game.GroupId,
                    text: $"Виправдовування завершено \n Ведучий може почати повторне голосування",
                    cancellationToken: cancellationToken);
            }
            else
            {
                GiveSpeakingTimeMaxVotes(game.MaxVotesUsers[i + 1], botClient, cancellationToken);
            }
            return;
        }
    }
}

async void CharactersMenu(BUser user, int index, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    string text = "";
    BGame game = user.BGame;
    BUser u = game.Users[index];
    text += $"'{u.Name}' ";
    if (u.IsVotedOut)
    {
        text += "Вигнанець ";
    }
    if (u.IsDead)
    {
        text += "Мертвий";
    }
    text += "\n";
    if (u.ProfessionOpened)
    {
        text += $"Професія | '{u.Profession.Name}'\n";
    }
    else
    {
        text += "Професія | ------\n";
    }
    if (u.BiologyOpened)
    {
        text += $"Біологія |'{u.Biology.Name}'\n";
    }
    else
    {
        text += "Біологія | ------\n";
    }
    if (u.HealthConditionOpened)
    {
        text += $"Стан здоров'я | '{u.HealthCondition.Name}'\n";
    }
    else
    {
        text += "Стан здоров'я | ------\n";
    }
    if (u.HobbyOpened)
    {
        text += $"Хоббі | '{u.Hobby.Name}'\n";
    }
    else
    {
        text += "Хоббі | ------\n";
    }
    if (u.LuggagesOpened)
    {
        text += $"Багаж | '{u.Luggages[0].Name}'\n";
    }
    else
    {
        text += "Багаж | ------\n";
    }
    if (u.Luggages.Count > 1)
    {
        text += $"Додатковий багаж | '{u.Luggages[1].Name}'\n";
    }
    if (u.AdditionalInfoOpened)
    {
        text += $"Факт | '{u.AdditionalInfo.Name}'\n";
    }
    else
    {
        text += "Факт | ------\n";
    }
    if (u.FirstSpecialCardUsed)
    {
        text += $"Спеціальна карта | '{u.SpecialCards[0].Name}'\n";
    }
    else
    {
        text += "Спеціальна карта | ------\n";
    }
    if (u.SecondSpecialCardUsed)
    {
        text += $"Додаткова спеціальна карта | '{u.SpecialCards[1].Name}'\n";
    }
    else if (!u.SecondSpecialCardUsed && u.SpecialCards.Count > 1)
    {
        text += "Додаткова спеціальна карта | ------\n";
    }
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: user.TelegramId,
                  text: text,
                  cancellationToken: cancellationToken);
    if (index > 0)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("%E2%97%80", $"'{user.Id}.{index - 1}'.'{sentMessage.MessageId}'_CharactersMenu"));
    }
    if (index < user.BGame.Users.Count - 1)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("%E2%96%B6", $"'{user.Id}.{index + 1}'.'{sentMessage.MessageId}'_CharactersMenu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Головне меню", $"'{user.Id}.{index + 1}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(user.TelegramId, sentMessage.MessageId, inlineKeyboard);
    db.SaveChanges();
}
async void GameInfoMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    string text = $"Катастрофа: '{user.BGame.Catastrophe.Name}'\n Бункер:\n";
    for (int i = 0; i < user.BGame.Status; i++)
    {
        text += $"\"'{user.BGame.BunkerInfos[i]}'\"\n";
    }
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: user.TelegramId,
                  text: text,
                  cancellationToken: cancellationToken);
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(user.TelegramId, sentMessage.MessageId, inlineKeyboard);
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
        case "Profesion":
            {
                user.ProfessionOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою професію: '{user.Profession.Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);

            }
            break;
        case "Biology":
            {
                user.BiologyOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою біологію: '{user.Biology.Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
            }
            break;
        case "Hobby":
            {
                user.HobbyOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває своє хоббі: '{user.Hobby.Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
            }
            break;
        case "Health":
            {
                user.HealthConditionOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свій стан здоров'я: '{user.HealthCondition.Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
            }
            break;
        case "AddInfo":
            {
                user.AdditionalInfoOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває факт про себе: '{user.AdditionalInfo.Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
            }
            break;
        case "Luggage":
            {
                user.LuggagesOpened = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває своій багаж: '{user.Luggages[0].Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
            }
            break;
        case "FirstSCard":
            {
                user.FirstSpecialCardUsed = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою спеціальну карту: '{user.SpecialCards[0].Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
                PauseGame(user.BGame.GroupId, botClient, cancellationToken);
            }
            break;
        case "SecondSCard":
            {
                user.SecondSpecialCardUsed = true;
                var userChat = await botClient.GetChatAsync(user.TelegramId);
                Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: user.BGame.GroupId,
                text: $"'{userChat.Username}' розкриває свою ДОДАТКОВУ спеціальну карту: '{user.SpecialCards[1].Name}'",
                cancellationToken: cancellationToken);
                MainMenu(user, botClient, cancellationToken);
                PauseGame(user.BGame.GroupId, botClient, cancellationToken);
            }
            break;

    }
}

void GiveNewCard(long userId, string type)
{
    List<Profession> listProfesions = new(db.Professions);
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
        case "Profesion":
            {
                bool uni = false;
                int i = -1;
                while (!uni)
                {
                    uni = true;
                    i = Random.Shared.Next(0, listProfesions.Count);
                    foreach (BUser u in user.BGame.Users)
                    {
                        if (u.Profession == listProfesions[i])
                        {
                            uni = false;
                        }
                    }
                }
                user.Profession = listProfesions[i];

            }
            break;
        case "Biology":
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
        case "Hobby":
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
        case "Health":
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
        case "AddInfo":
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
        case "Luggage":
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
        case "Biology":
            {
                (user2.Biology, user1.Biology) = (user1.Biology, user2.Biology);
            }
            break;
        case "Hobby":
            {
                (user2.Hobby, user1.Hobby) = (user1.Hobby, user2.Hobby);
            }
            break;
        case "Health":
            {
                (user2.HealthCondition, user1.HealthCondition) = (user1.HealthCondition, user2.HealthCondition);
            }
            break;
        case "AddInfo":
            {
                (user2.AdditionalInfo, user1.AdditionalInfo) = (user1.AdditionalInfo, user2.AdditionalInfo);
            }
            break;
        case "11Luggage":
            {
                (user2.Luggages[0], user1.Luggages[0]) = (user1.Luggages[0], user2.Luggages[0]);
            }
            break;
        case "12Luggage":
            {
                (user2.Luggages[1], user1.Luggages[0]) = (user1.Luggages[0], user2.Luggages[1]);
            }
            break;
        case "21Luggage":
            {
                (user2.Luggages[0], user1.Luggages[1]) = (user1.Luggages[1], user2.Luggages[0]);
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
        case "Biology":
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
        case "Hobby":
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
        case "Health":
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
        case "AddInfo":
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
        case "Luggage":
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
    GiveNewCard(user2Id, "Card");
    db.SaveChanges();
}


async void AdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Меню Ведучого",
                  cancellationToken: cancellationToken);
    InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithCallbackData("Почати голосування", $"'{user.Id}'_startVoting"),
                        InlineKeyboardButton.WithCallbackData("ПАУЗА", $"'{user.Id}'_pauseGame"),
                        InlineKeyboardButton.WithCallbackData("ПРИБРАТИ З ПАУЗИ", $"'{user.Id}'_unpauseGame"),
                        InlineKeyboardButton.WithCallbackData("Розкрити характеристику гравця",$"'{user.Id}'.'{sentMessage.MessageId}'_OpenCardMenu"),
                        InlineKeyboardButton.WithCallbackData("Змінити Характеристику гравця", $"'{user.Id}'.'{sentMessage.MessageId}'_ChangeCardMenu"),
                        InlineKeyboardButton.WithCallbackData("Обміняти Характеристики двох гравців", $"'{user.Id}'.'{sentMessage.MessageId}'_SwapCardsMenu"),
                        InlineKeyboardButton.WithCallbackData("Перемішати Характеристику відкритих гравців", $"'{user.Id}'.'{sentMessage.MessageId}'_ShuffleCardsMenu"),
                        InlineKeyboardButton.WithCallbackData("Вкрасти багаж", $"'{user.Id}'.'{sentMessage.MessageId}'_StealLuggageMenu"),
                        InlineKeyboardButton.WithCallbackData("Змінити Характеристику Бункера", $"'{user.Id}'.'{sentMessage.MessageId}'_BunkerCardMenu"),
                        InlineKeyboardButton.WithCallbackData("Передати роль ведучого", $"'{user.Id}'.'{sentMessage.MessageId}'_NewAdminMenu"),
                        InlineKeyboardButton.WithCallbackData("Редагувати голоси",$"'{user.Id}'.'{sentMessage.MessageId}'_VotesMenu"),
                        InlineKeyboardButton.WithCallbackData("ГОЛОВНЕ МЕНЮ", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu")
                    });
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void SendOpenCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_OpenCard2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void SendOpenCard2AdminMenu(BUser admin, BUser target, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"'{target.Name}'|Оберіть характеристику яку бажаєте відкрити",
                  cancellationToken: cancellationToken);
    if (!target.ProfessionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Професія", $"'{target.Id}'.'{sentMessage.MessageId}'_openProfession"));
    }
    if (!target.BiologyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Біологія", $"'{target.Id}'.'{sentMessage.MessageId}'_openBiology"));
    }
    if (!target.HealthConditionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Стан здоров'я", $"'{target.Id}'.'{sentMessage.MessageId}'_openHealth"));
    }
    if (!target.HobbyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Хоббі", $"'{target.Id}'.'{sentMessage.MessageId}'_openHobby"));
    }
    if (!target.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Багаж", $"'{target.Id}'.'{sentMessage.MessageId}'_openFirstLuggage"));
    }
    if (!target.AdditionalInfoOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Факт", $"'{target.Id}'.'{sentMessage.MessageId}'_openAddInfo"));
    }
    if (!target.FirstSpecialCardUsed)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Спеціальна карта", $"'{target.Id}'.'{sentMessage.MessageId}'_openFirstSCard"));
    }
    if (target.SpecialCards.Count > 1 && !target.SecondSpecialCardUsed)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"ДОДАТКОВА спеціальна карта", $"'{target.Id}'.'{sentMessage.MessageId}'_openSecondSCard"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору гравця", $"'{admin.Id}'.'{sentMessage.MessageId}'_OpenCardMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void SendNewCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_ChangeCard2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void SendNewCard2AdminMenu(BUser admin, BUser target, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"'{target.Name}'|Оберіть характеристику",
                  cancellationToken: cancellationToken);
    if (target.ProfessionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Професія '{target.Profession.Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeProfession"));
    }
    if (target.BiologyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Біологія '{target.Biology.Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeBiology"));
    }
    if (target.HealthConditionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Стан здоров'я '{target.HealthCondition.Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeHealth"));
    }
    if (target.HobbyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Хоббі '{target.Hobby.Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeHobby"));
    }
    if (target.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Багаж '{target.Luggages[0].Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeFirstLuggage"));
    }
    if (target.Luggages.Count > 1)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Додатковий Багаж '{target.Luggages[1].Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeSecondLuggage"));
    }
    if (target.AdditionalInfoOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Факт '{target.AdditionalInfo.Name}'", $"'{target.Id}'.'{sentMessage.MessageId}'_changeAddInfo"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору гравця", $"'{admin.Id}'.'{sentMessage.MessageId}'_ChangeCardMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void SwapCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть першого гравця",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_SwapCard2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void SwapCard2AdminMenu(BUser admin, BUser target1, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть другого гравця",
                  cancellationToken: cancellationToken);
    foreach (BUser u in admin.BGame.Users)
    {
        if (u != target1)
        {
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{admin.Id}'.'{u.Id}'.'{target1.Id}'.'{sentMessage.MessageId}'_SwapCard3Menu"));
        }
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору 1 гравця", $"'{admin.Id}'.'{sentMessage.MessageId}'_SwapCardsMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void SwapCard3AdminMenu(BUser admin, BUser target1, BUser target2, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"'{target1.Name}','{target2.Name}'|Оберіть характеристику",
                  cancellationToken: cancellationToken);
    if (target1.ProfessionOpened && target2.ProfessionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Професія ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swapProfession"));
    }
    if (target1.BiologyOpened && target2.BiologyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Біологія ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swapBiology"));
    }
    if (target1.HealthConditionOpened && target2.HealthConditionOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Стан здоров'я ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swapHealth"));
    }
    if (target1.HobbyOpened && target2.HobbyOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Хоббі ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swapHobby"));
    }
    if (target1.LuggagesOpened && target2.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Основний багаж '{target1.Name}' та основний багаж '{target2.Name}' ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swap11Luggage"));
    }
    if (target1.LuggagesOpened && target2.Luggages.Count > 1)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Основний багаж '{target1.Name}' та додатковий багаж '{target2.Name}' ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swap12Luggage"));
    }
    if (target1.Luggages.Count > 1 && target2.LuggagesOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Додатковий багаж '{target1.Name}' та основний багаж '{target2.Name}' ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swap21Luggage"));
    }
    if (target1.AdditionalInfoOpened && target2.AdditionalInfoOpened)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Факт ", $"'{target1.Id}'.'{target2.Id}'.'{sentMessage.MessageId}'_swapAddInfo"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору гравців", $"'{admin.Id}'.'{sentMessage.MessageId}'_SwapCardsMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void ShuffleCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"Оберіть характеристику",
                  cancellationToken: cancellationToken);
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті професії", $"'{user.Id}'.'{sentMessage.MessageId}'_shuffleProfession"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Вся відкрита біологія", $"'{user.Id}'.'{sentMessage.MessageId}'_shuffleBiology"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті стани здоров'я", $"'{user.Id}'.'{sentMessage.MessageId}'_shuffleHealth"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті хоббі ", $"'{user.Id}'.'{sentMessage.MessageId}'_shuffleHobby"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті багажі", $"'{user.Id}'.'{sentMessage.MessageId}'_shuffleLuggage"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Всі відкриті факти ", $"'{user.Id}'.'{sentMessage.MessageId}'_shuffleAddInfo"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void StealLuggageAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця, що краде багаж",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_StealLuggage2Menu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void StealLuggage2AdminMenu(BUser admin, BUser stealer, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"'{stealer.Name}'| Оберіть ціль крадіжки",
                  cancellationToken: cancellationToken);
    foreach (BUser u in admin.BGame.Users)
    {
        if (u != stealer)
        {
            if (u.LuggagesOpened)
            {
                keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Luggages[0].Name}' у '{u.Name}'", $"'{stealer.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_stealLuggage"));
            }
        }
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися до вибору крадія", $"'{admin.Id}'.'{sentMessage.MessageId}'_StealLuggageMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void BunkerCardAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"Оберіть характеристику бункера",
                  cancellationToken: cancellationToken);
    for (int i = 0; i < user.BGame.Status; i++)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{user.BGame.BunkerInfos[i].Name}'", $"'{user.Id}'.'{i}'.'{sentMessage.MessageId}'_BunkerCard2Menu"));
    }
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void BunkerCard2AdminMenu(BUser user, int cardNumber, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: $"\"'{user.BGame.BunkerInfos[cardNumber].Name}'\"\n Що зробити з картою?",
                  cancellationToken: cancellationToken);
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Замінити на нову", $"'{user.Id}'.'{cardNumber}'.'{sentMessage.MessageId}'_newBunkerCard"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Скинути", $"'{user.Id}'.'{cardNumber}'.'{sentMessage.MessageId}'_removeBunkerCard"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void GiveAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця, якому передати права Ведучого",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_giveAdmin"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sentMessage.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}

async void VotesAdminMenu(BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = user.TelegramId;
    string text = "";
    List<InlineKeyboardButton> keyboardButtons = new();
    foreach (BUser u in user.BGame.Users)
    {
        text += $"За гравця '{u.Name}' проголосували: \n";
        foreach (BUser u2 in user.BGame.Users)
        {
            if (u2.VotedFor == u)
            {
                if (u2.IsVoteDoubled)
                {
                    text += "Подвійний голос|";
                }
                text += $"'{u2.Name}'\n";
            }
        }
    }
    Message sent1Message = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: text,
                  cancellationToken: cancellationToken);
    Message sent2Message = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця, чий голос бажаєте редагувати",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sent1Message.MessageId}'.'{sent2Message.MessageId}'_Votes2AdminMenu"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Головне меню", $"'{user.Id}'.'{sent1Message.MessageId}'.'{sent2Message.MessageId}'_MainMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sent2Message.MessageId, inlineKeyboard);
}
async void Votes2AdminMenu(BUser admin, BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Що бажаєте зробити з голосом",
                  cancellationToken: cancellationToken);
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Зміна вибору", $"'{admin.Id}'.'{user.Id}'.'{sentMessage.MessageId}'_Votes3AdminMenu"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Подвоєння голосу", $"'{admin.Id}'.'{user.Id}'.'{sentMessage.MessageId}'_doubleVote"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Відміна голосу", $"'{admin.Id}'.'{user.Id}'.'{sentMessage.MessageId}'_cancelVote"));
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися", $"'{admin.Id}'.'{sentMessage.MessageId}'_VotesMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
    await botClient.EditMessageReplyMarkupAsync(chatId, sentMessage.MessageId, inlineKeyboard);
}
async void Votes3AdminMenu(BUser admin, BUser user, ITelegramBotClient botClient, CancellationToken cancellationToken)
{
    var chatId = admin.TelegramId;
    List<InlineKeyboardButton> keyboardButtons = new();
    
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  text: "Оберіть гравця, проти якого голосуєте",
                  cancellationToken: cancellationToken);
    foreach (BUser u in user.BGame.Users)
    {
        keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"'{u.Name}'", $"'{user.Id}'.'{u.Id}'.'{sentMessage.MessageId}'_voteAdmin"));
    }
    keyboardButtons.Add(InlineKeyboardButton.WithCallbackData($"Повернутися", $"'{admin.Id}'.'{user.Id}'.'{sentMessage.MessageId}'_Votes2AdminMenu"));
    InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
}
