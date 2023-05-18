using BunkerBot;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var db = new AppDbContext();
db.Database.EnsureCreated();


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
                if (JoinUser(gameChatId, chatId))
                {
                    Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"You joined a game in chat '{chatTitle}'",
                cancellationToken: cancellationToken);
                }
                Console.WriteLine($"Received a '{message.Text}' message in chat {chatId}.");
            }

        }
        if (update.Message.Chat.Type == ChatType.Supergroup)
        {
            var chatId = update.Message.Chat.Id;
            switch (message.Text)
            {
                case "/createGame":
                    {
                        CreateGame(chatId);
                        InlineKeyboardMarkup inlineKeyboard = new(new[]{
                        InlineKeyboardButton.WithUrl("Join Game",$"https://t.me/{botClient.GetMeAsync().Result.Username}?start=chatId={chatId}"),
                    });
                        Message sentMessage = await botClient.SendTextMessageAsync(
                                  chatId: chatId,
                                  text: $"Press to join",
                                  replyMarkup: inlineKeyboard,
                                  cancellationToken: cancellationToken);
                    }
                    break;
                case "/startGame":
                    StartGame(chatId);
                    SendStatsToAll (GetGame(chatId).Id,botClient);
                    break;
                default:
                    {
                        Message sentMessage = await botClient.SendTextMessageAsync(
                                 chatId: chatId,
                                 text: $"Wrong command",
                                 cancellationToken: cancellationToken);
                    }
                    break;
            }





        }
    }

    //   if (update.Type == UpdateType.Message)
    //   {
    //
    //       var messageSplited = message.Text.Split(new string[] { "chatId=" }, StringSplitOptions.None);
    //       var chatId = message.Chat.Id;
    //       if (messageSplited.Length > 1)
    //       {
    //           InlineKeyboardMarkup inlineKeyboard = new(new[]{
    //           InlineKeyboardButton.WithCallbackData("Get Stats",$"getStats"),
    //           });
    //           var gameChatId = long.Parse(messageSplited[1]);
    //           var chatTitle = botClient.GetChatAsync(gameChatId).Result.Title;
    //           if(AddUser(gameChatId, chatId))
    //           {
    //               Message sentMessage = await botClient.SendTextMessageAsync(
    //           chatId: chatId,
    //           text: $"You joined a game in chat '{chatTitle}'",
    //           replyMarkup: inlineKeyboard,
    //           cancellationToken: cancellationToken);
    //               
    //           }
    //           Console.WriteLine($"Received a '{message.Text}' message in chat {chatId}.");
    //       }
    //       else
    //       {
    //           InlineKeyboardMarkup inlineKeyboard = new(new[]{
    //           InlineKeyboardButton.WithUrl("Join Game",$"https://t.me/{botClient.GetMeAsync().Result.Username}?start=chatId={chatId}"),
    //       });
    //           Message sentMessage = await botClient.SendTextMessageAsync(
    //           chatId: chatId,
    //           text: $"Press to join",
    //           replyMarkup: inlineKeyboard,
    //           cancellationToken: cancellationToken);
    //           Console.WriteLine($"Received a '{message.Text}' message in chat {chatId}.");
    //       }
    //       
    //
    //
    //       
    //   }
    //   if (update.Type == UpdateType.CallbackQuery)
    //   {
    //
    //       var callback = update.CallbackQuery;
    //       var chatId = callback.Message.Chat.Id;
    //       InlineKeyboardMarkup inlineKeyboard = new(new[]
    //{//
     //   new []
     //   {
     //       InlineKeyboardButton.WithUrl("test",$"https://t.me/{botClient.GetMeAsync().Result.Username}?start=chatId={chatId}"),
     //   }
    //}//);
     //       var pressedButton = callback.Data;
     //       Message sentMessage = await botClient.SendTextMessageAsync(
     //           chatId: chatId,
     //           text: $"You pressed a '{pressedButton}'",
     //           replyMarkup: inlineKeyboard,
     //           cancellationToken: cancellationToken);
     //       Console.WriteLine($"{pressedButton} is pressed in chat {chatId}.");
     //   }
     //
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
bool JoinUser(long chatId, long userId)
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

        if (game.GroupId == chatId)
        {
            var user = db.Users.FirstOrDefault(d => d.TelegramId == userId);
            if (user == null)
            {
               user= AddUser(userId);
            }
            user.BGame = game;
            db.SaveChanges();
            return true;
        }
    }
    return false;
}

BUser AddUser(long userId)
{
    BUser user = new BUser();
    user.TelegramId = userId;
    db.Users.Add(user);
    db.SaveChanges();
    return user;
}
void CreateGame(long chatId)
{
    BGame game = new BGame();
    game.GroupId = chatId;
    db.Games.Add(game);
    db.SaveChanges();
}

bool StartGame(long chatId)
{
    BGame? game = db.Games.FirstOrDefault(d => d.GroupId == chatId);
    if (game == null)
    {
        return false;
    }
    GiveStats(game.Id);
    db.SaveChanges();
    return true;
}
void GiveStats(int gameId)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    List<Profession> listProffesions = new (db.Professions);
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
        user.AdditionalInfo = (listAdditionalInfos [i]);
        listAdditionalInfos.RemoveAt(i);
    }
}

void SendStatsToAll(int gameId, ITelegramBotClient botClient)
{
    BGame? game = db.Games.Find(gameId);
    if (game == null)
        return;
    foreach (BUser user in game.Users)
    {
        SendStats(user, botClient);
    }
}
async void SendStats(BUser user, ITelegramBotClient botClient)
{
    
        string ProfMarker= ":full_moon_with_face:";
        string BioMarker = ":full_moon_with_face:";
        string HealthMarker = ":full_moon_with_face:";
        string HobbyMarker = ":full_moon_with_face:";
        string LugMarker = ":full_moon_with_face:";
        string InfoMarker = ":full_moon_with_face:";
        string FCardMarker= ":full_moon_with_face:";
        string SCardMarker = ":full_moon_with_face:";
        List<InlineKeyboardButton> keyboardButtons = new();
        if (!user.ProfessionOpened)
        {
            ProfMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Proffesion", "sProf"));
        }
        if (!user.BiologyOpened)
        {

            BioMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Biology", "sBio"));
        }
        if (!user.HealthConditionOpened)
        {
            HealthMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Health Condition", "sHealth"));
        }
        if (!user.HobbyOpened)
        {
            HobbyMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Hobby", "sHobby"));
        }
        if (!user.LuggagesOpened)
        {
            LugMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Luggage", "sLugg"));
        }
        if (!user.AdditionalInfoOpened)
        {
            InfoMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Additional Info", "sInfo"));
        }
        if (!user.FirstSpecialCardUsed)
        {
            FCardMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show Special Card", "sFCard"));
        }
        if(user.SpecialCards.Count>1 && !user.SecondSpecialCardUsed)
        {
            SCardMarker = ":new_moon_with_face:";
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("Show ADDITIONAL Special Card", "sSCard"));
        }
        InlineKeyboardMarkup inlineKeyboard = new(keyboardButtons);
        long chatId = user.TelegramId;
        string Lugtext=string.Join(" ",user.Luggages.Select(d=>d.Name));
        string text = $"Your stats:\n " +
                      $"'{ProfMarker}'|Proffesion: '{user.Profession!.Name}'\n" +
                      $"'{BioMarker}'|Biology: '{user.Biology!.Name}'\n" +
                      $"'{HealthMarker}'|Health Condition: '{user.HealthCondition!.Name}'\n" +
                      $"'{HobbyMarker}'|Hobby:'{user.Hobby!.Name}'\n" +
                      $"'{LugMarker}'|Luggage: '{user.Luggages[0].Name}'\n" +
                      $"'{InfoMarker}'|Additional Info: '{user.AdditionalInfo!.Name}'\n" +
                      $"'{FCardMarker}'|Special card: '{user.SpecialCards[0]!.Name}'\n";
        if (user.SpecialCards.Count > 1)
        {
            text += $"'{SCardMarker}'|ADDITIONAL Special card: '{user.SpecialCards[1]!.Name}'";
        }
    Message sentMessage = await botClient.SendTextMessageAsync(
                  chatId: chatId,
                  replyMarkup:inlineKeyboard,
                  text: text);
    
}


BUser? GetUser(long userChatId)
{
    return db.Users.FirstOrDefault(d => d.TelegramId == userChatId);
}
BGame? GetGame(long chatId)
{
    return db.Games.FirstOrDefault(d => d.GroupId==chatId);
}

void GiveNewCard(long userId, string type)
{
    List<Profession> listProffesions = new(db.Professions);
    List<Hobby> listHobbies = new(db.Hobbies);
    List<Luggage> listLuggages = new(db.Luggages);
    List<HealthCondition> listHealthConditions = new(db.HealthConditions);
    List<Biology> listBiologies = new(db.Biologies);
    List<AdditionalInfo> listAdditionalInfos = new(db.AdditionalInfo);
    List<SpecialCard> listSpecialCards=new(db.SpecialCards);
    BUser? user = db.Users.Find(userId);
    if (user == null)
        return;
    
        
    switch (type)
    {
        case "Prof":
            {
                bool uni = false;
                int i=-1;
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
                        foreach(Luggage l in u.Luggages)
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
                user.SpecialCards[1] = listSpecialCards[i];
            }
            break;
    }
    db.SaveChanges();

}

void SwapCards(long user1Id, long user2Id, string type)
{
    BUser? user1 = db.Users.Find(user1Id);
    BUser? user2 = db.Users.Find(user2Id);
    if (user1 != null ||user2==null)
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


