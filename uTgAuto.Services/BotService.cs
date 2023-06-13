using dotenv.net;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using uTgAuto.Services.Models;
using File = System.IO.File;
using Message = uTgAuto.Services.Models.Message;
using User = uTgAuto.Services.Models.User;

namespace uTgAuto.Services
{
    public class BotService
    {
        public static DatabaseService databaseService = new DatabaseService();
        private readonly TelegramBotClient? _client;
        private List<User> _users = new List<User>();

        public BotService()
        {
            try
            {
                DotEnv.Load();

                string apiKey = Environment.GetEnvironmentVariable("API_KEY")!;
                _client = new TelegramBotClient(apiKey);

                var receiveOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>()
                };
                Task.Run(async () =>
                {
                    _client.StartReceiving(handleUpdateAsync, handleErrorAsync, receiveOptions);

                    _users = databaseService.GetUsers();

                    await initialize();
                }).Wait();
            }
            catch (Exception ex)
            {
                LoggerService.Error($"BotService.BotService: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        private async Task initialize()
        {
            try
            {
                if (_client == null) return;

                var me = await _client.GetMeAsync();
                try
                {
                    LoggerService.Info($"Start listening for @{me.Username}");
                    LoggerService.Info("Deleting all Session files...");

                    foreach (string file in Directory.GetFiles(Environment.CurrentDirectory, ".\\Sessions\\*.session"))
                    {
                        try
                        {
                            File.Delete(file);
                            LoggerService.Info($"Deleted file: {file}");
                        }
                        catch (Exception ex)
                        {
                            LoggerService.Error($"Error deleting file: {file}. {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!ex.ToString().Contains("Sessions"))
                        LoggerService.Debug(ex.ToString());
                }

                /* Go Through All Users to Say that They Need to connect */
                foreach (var user in _users)
                {
                    if (user.State == UserState.SignInTelegramService)
                    {
                        await _client.SendTextMessageAsync(user.ChatID, "Наш бот перезапускался! Вам нужно заново подключить(/connect) свой аккаунт чтобы возобновить пользование им! Если вы уже зарегегистрированы, то регистрроваться не нужно!");
                    }
                }
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                LoggerService.Error($"BotService.initialize: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        private Task handleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            LoggerService.Error($"BotService.handleErrorAsync: [{exception.GetLine()}] [{exception.Source}]\n\t{exception.Message}");

            return Task.CompletedTask;
        }

        private async Task handleUpdateAsync(ITelegramBotClient client, Telegram.Bot.Types.Update update, CancellationToken token)
        {
            try
            {
                if (_client == null) return;

                switch (update.Type)
                {
                    case UpdateType.Message:
                        var message = update.Message;
                        if (message == null || message!.From == null || message!.Text == null || message!.Sticker != null) return;

                        var user = _users.Find(u => u.ChatID == message.From.Id);

                        #region /start

                        if (message!.Text!.Contains("/start"))
                        {
                            try
                            {
                                _users = databaseService.GetUsers();
                                bool isSignedUp = _users.Find(u => u.ChatID == message.From!.Id && u.ApiID != 0) != null;
                                bool isNew = true;
                                try
                                {
                                    isNew = !_users.Find(u => u.ChatID == message.From!.Id)!.IsSignedUp;
                                }
                                catch { }

                                if (message!.Text!.Length > 7)
                                {
                                    var startParameter = message!.Text!.Split(" ")[1].ToString();

                                    if (startParameter.Contains("code") && user != null)
                                    {
                                        if (databaseService.GetUserByChatID(user.ChatID).Code.Length == 5)
                                        {   
                                            if (user.Messages == null || user.Messages.Count == 0)
                                            {
                                                await _client.SendTextMessageAsync(user.ChatID, "Ваш код получен!");
                                                await _client.SendTextMessageAsync(user.ChatID, "Теперь последний шаг перед запуском бота, вам нужно предоставить сообщения которые он будет отправлять. Я вам с этим помогу!\n\nУкажите текст который ваш бот будет отправлять. Например: Привет!");

                                                user.State = UserState.MessageText;
                                                _users.Find(u => u.ChatID == message.From.Id)!.State = UserState.MessageText;
                                                databaseService.UpdateUser(user);
                                            }
                                            else
                                            {
                                                await _client.SendTextMessageAsync(user.ChatID, "Ваш код получен! /start чтобы запустить бота");
                                            
                                                user.State = UserState.Ready;
                                                user.TelegramService = new TelegramService(user.Messages, user.ParallelMessages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID, user.Coins);
                                                var result = user.TelegramService.Connect().Result;
                                    
                                                if (result != null && result.Message!.Contains("FLOOD"))
                                                {
                                                    await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                                    return;
                                                }
                                                else if (result != null && result.IsEnoughCoins == false)
                                                {
                                                    await _client.SendTextMessageAsync(user.ChatID, $"У вас недостаточно монет({user.Coins}).");

                                                    user.State = UserState.Start;
                                                    user.TelegramService!.Disconnect();
                                                    user.TelegramService = null;
                                                    databaseService.UpdateUser(user);

                                                    await _client.SendTextMessageAsync(user.ChatID, $"Отключаюсь! /connect чтобы заново подключиться!");
                                                    return;
                                                }

                                                if (user.TelegramService.IsConnected)
                                                {
                                                    await _client.SendTextMessageAsync(user.ChatID, "Вы успешно подключили ваш аккаунт к боту. Теперь напишите /start чтобы запустить его!");
                                                }
                                                else
                                                {
                                                    await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так... Попробуйте подключиться заново /conect");
                                                }

                                                databaseService.UpdateUser(user);
                                            }
                                        }
                                        else
                                        {
                                            await _client.SendTextMessageAsync(user.ChatID, "Код неверный!");
                                        }
                                    }
                                    return;
                                }

                                if (isSignedUp == false)
                                {
                                    if (message!.Text!.Length > 7)
                                    {
                                        var startParameter = message!.Text!.Split(" ")[1].ToString();

                                        long chatId;
                                        bool isInt = long.TryParse(startParameter, out chatId);

                                        if (isInt)
                                        {
                                            var chatIdOwner = _users.Find(u => u.ChatID == chatId);

                                            if (chatIdOwner != null)
                                            {
                                                chatIdOwner.Coins++;
                                                databaseService.UpdateUser(chatIdOwner);
                                                await _client.SendTextMessageAsync(chatId, $"По вашей реферальной ссылке присоединился 1 пользователь. За это вы получаете 1 монету!\n\nВведите /coins чтобы посмотреть ваш баланс.");
                                            }
                                            else
                                            {
                                                await _client.SendTextMessageAsync(message.From!.Id, $"Вы перешли по реферальной ссылке несуществуещего пользователя({startParameter})!");
                                            }
                                        }
                                        else
                                        {
                                            await _client.SendTextMessageAsync(message.From!.Id, $"Неизвестный аргумент {startParameter}!");
                                        }
                                    }
                                    else
                                    {
                                        if (isNew)
                                        {
                                            user = new User()
                                            {
                                                ChatID = message.From.Id,
                                                State = UserState.SignUpVerify,
                                                Coins = 1f,
                                                IsSignedUp = true
                                            };
                                            _users.Add(user);
                                            databaseService.AddUser(user);
                                        }
                                        else
                                        {
                                            user = _users.Find(u => u.ChatID == message.From.Id);
                                            user!.State = UserState.SignUpVerify;
                                            databaseService.AddUser(user);
                                        }
                                        string agreementLink = "https://hackmd.io/@uaquax/uTgAuto";
                                        string messageText = $"Внимание: бот на данные момент находится в тестировании, могут встречаться неполадки /support чтобы написать нам\n\nДобро Пожаловать, {message.From.FirstName} {message.From.LastName}! Это бот для автоматизации отправки сообщений интегрированный с ИИ! \n\nПеред тем как зарегистрироваться, вы должны согласиться с <a href='{agreementLink}'>пользовательским соглашением</a> \n\nДля автоматизации наш бот подключиться к вашему аккаунту.\n\nЕсли вы согласны напишите <b>да,</b> (или нажмите на кнопку ниже) если не согласны напишите <b>нет,</b> (или нажмите на кнопку ниже) и мы удалим всю существующую о вас информацию из нашей базы данных.";

                                        var parseMode = ParseMode.Html;
                                        var messageEntities = new[]
                                        {
                                            new MessageEntity { Type = MessageEntityType.Bold, Offset = messageText.IndexOf("<b>"), Length = 3 },
                                            new MessageEntity { Type = MessageEntityType.Bold, Offset = messageText.IndexOf("<b>", messageText.IndexOf("<b>") + 1), Length = 3 },
                                            new MessageEntity { Type = MessageEntityType.TextLink, Offset = messageText.IndexOf(agreementLink), Length = agreementLink.Length, Url = agreementLink }
                                        };
                                        var inlineKeyboard = new InlineKeyboardMarkup(
                                            new[]
                                            {
                                                new[]
                                                {
                                                    InlineKeyboardButton.WithUrl("Пользовательское соглашение", agreementLink)
                                                },
                                                new[]
                                                {
                                                    InlineKeyboardButton.WithCallbackData("Да, я согласен", $"yes"),
                                                    InlineKeyboardButton.WithCallbackData("Нет, я не согласен", $"no")
                                                }
                                            }
                                        );
                                        await _client.SendTextMessageAsync(user.ChatID, messageText, parseMode: parseMode, entities: messageEntities, replyMarkup: inlineKeyboard);
                                        
                                        return;
                                    }
                                }
                                if (user!.Coins > 0 && user!.Messages != null && user!.Messages.Count != 0)
                                {
                                    user.State = UserState.Running;
                                    databaseService.UpdateUser(user);

                                    await _client.SendTextMessageAsync(message.From!.Id, $"Запускаю! Доступно монет(1 цикл сообщений = 0.001 монет): {user.Coins}");

                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            var result = user.TelegramService!.Start().Result;

                                            if (result != null && result.Message!.Contains("FLOOD"))
                                            {
                                                await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                                return;
                                            }
                                            else if (result != null && result.IsEnoughCoins == false)
                                            {
                                                await _client.SendTextMessageAsync(user.ChatID, $"У вас недостаточно монет({user.Coins}).");
                                               
                                                user.State = UserState.Start;
                                                user.TelegramService!.Disconnect();
                                                user.TelegramService = null;
                                                databaseService.UpdateUser(user);

                                                await _client.SendTextMessageAsync(user.ChatID, $"Отключаюсь! /connect чтобы заново подключиться!");
                                                return;
                                            }
                                        }
                                        catch { }
                                    });

                                    await _client.SendTextMessageAsync(message.From.Id, "Запустил!");
                                }
                                else if (user!.Coins <= 0)
                                {
                                    await _client.SendTextMessageAsync(message.From!.Id, $"Недостаточно монет({user.Coins})! Пригласи друга по реферальной ссылке или купи новые монеты.");
                                }
                                else if (user!.Messages == null || user!.Messages.Count <= 0)
                                {
                                    await _client.SendTextMessageAsync(message.From!.Id, $"Вы не создали ни одного сообщения! Введите /new_message чтобы создать сообщение!");
                                }
                            }
                            catch { }

                            return;
                        }

                        #endregion

                        #region /connect

                        if (message.Text!.Contains("/connect"))
                        {
                            if (user!.State == UserState.SignInTelegramService)
                            {
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        File.Delete($"Sessions/{user.ChatID}.session");
                                    }
                                    catch { }

                                    user.State = UserState.Ready;
                                    user.TelegramService = new TelegramService(user.Messages, user.ParallelMessages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID, user.Coins);
                                    var result = user.TelegramService.Connect().Result;
                                    if (result != null && result.Message!.Contains("FLOOD"))
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                        return;
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                                    {
                                        new[]
                                        {
                                            InlineKeyboardButton.WithUrl("Ввести код", $"127.0.0.1:8080?chat_id={user.ChatID}")
                                        }
                                     });

                                    await _client.SendTextMessageAsync(user.ChatID, $"Нажмите на кнопку ниже чтобы ввести код полученный от Телеграма(это нужно чтобы подключиться к аккаунту): ", replyMarkup: inlineKeyboard);
                                    await _client.SendTextMessageAsync(user.ChatID, $"После того как ввели ваш код, напишите /start чтобы запустить бота!");
                                }).Wait();
                                return;
                            }
                        }

                        #endregion


                        #region /disconnect

                        if (message.Text.Contains("/disconnect"))
                        {
                            _ = Task.Run(async () =>
                            {
                                user!.State = UserState.Start;
                                user.TelegramService!.Disconnect();
                                user.TelegramService = null;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Отключаюсь! /connect чтобы заново подключиться!");
                            });
                        }

                        #endregion

                        #region /referral

                        if (message.Text.Contains("/referral"))
                        {
                            await _client.SendTextMessageAsync(user!.ChatID, $"Вот ваша реферальная ссылка: \n\nhttps://t.me/uTgAutoBot?start={user.ChatID}");
                            return;
                        }

                        #endregion

                        #region /support

                        if (message.Text.Contains("/support"))
                        {

                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new[]
                                {
                                    InlineKeyboardButton.WithUrl("Написать в поддержку", "https://t.me/uTgAutoSupport")
                                }
                            });
                            await _client.SendTextMessageAsync(user!.ChatID, $"Нажмите на кнопку ниже чтобы написать в поддержку: ", replyMarkup: inlineKeyboard);
                            return;
                        }

                        #endregion

                        #region /reset

                        if (message.Text.Contains("/reset") && message.Text.Contains("messages") == false)
                        {
                            try
                            {
                                databaseService.DeleteUser(user!.ChatID);
                                _users.Remove(_users.Find(u => u.ChatID == user.ChatID)!);

                                await _client.SendTextMessageAsync(user!.ChatID, $"Мы уважаем ваше решение! Ваш аккаунт удален! Чтобы зарегистрироваться заново вам нужно ввести /start");
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                            }
                            
                            return;
                        }

                        #endregion

                        #region /coins

                        if (message.Text.Contains("/coins"))
                        {
                            await _client.SendTextMessageAsync(user!.ChatID, $"Ваш баланс: {user.Coins}");
                        }

                        #endregion

                        #region /new_message

                        if (message.Text.Contains("/new_message"))
                        {
                            try
                            {
                                user!.State = UserState.MessageText;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Укажите текст который ваш бот будет отправлять. Например: Привет!");

                                return;
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user!.ChatID, "Что-то пошло не так!");
                            }
                        }

                        #endregion

                        #region /new_parallel_message

                        if (message.Text.Contains("/new_parallel_message"))
                        {
                            try
                            {
                                user!.State = UserState.ParallelMessageTargets;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Пример 1(Бот реагирует на сообщения содержащие слова привет, ку,хай: привет,ку,хай\nПример 2(Бот реагирует на сообщения содержащие цифры): @int\nПример 3(Бот реагирует на ЛЮБЫЕ сообщения): @string\nПример 4(Бот реагирует на сообщения содержащие ССЫЛКУ или НИКНЕЙМ): @link\nПример 5(Бот реагирует на сообщения содержащие утверждение(да, конечно, 100%, естественно, и тп. ): @yes\nПример 6(Бот реагирует на сообщения содержащие отрицание(нет, неа, и тп.)): @no\n\nВажно! \nЕсли вы хотите отвечать на любое сообщение, то введите @string \nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие цифры, то введите @int\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие ссылку(или телеграм аккаунт), то введите @link\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие утверждение(да), то введите @yes\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие отрицание(нет), то введите @no\n\nВведите текст на который будет отвечать бот(разделяя через запятую БЕЗ ПРОБЕЛА): ");

                                return;
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user!.ChatID, "Что-то пошло не так!");
                            }
                        }

                        #endregion

                        #region /reset_messages

                        if (message.Text.Contains("/reset_messages"))
                        {
                            try
                            {
                                user!.State = UserState.Ready;
                                user!.Messages.Clear();
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Вы очистили сообщения! /new_message чтобы добавить новое!");

                                return;
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user!.ChatID, "Что-то пошло не так!");
                            }
                        }

                        #endregion

                        #region /reset_parallel_messages

                        if (message.Text.Contains("/reset_parallel_messages"))
                        {
                            try
                            {
                                user!.State = UserState.Ready;
                                user!.ParallelMessages.Clear();
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Вы очистили паралельные сообщения! /new_parallel_message чтобы добавить новое!");

                                return;
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user!.ChatID, "Что-то пошло не так!");
                            }
                        }

                        #endregion

                        if (user == null) return; 

                        #region SignUpVerify

                        if (user.State == UserState.SignUpVerify)
                        {
                            var isYes = TelegramService.yesList.Any(yesWord => message.Text.ToLower().Contains(yesWord));
                            var isNo = TelegramService.noList.Any(noWord => message.Text.Contains(noWord));

                            if (isYes)
                            {
                                user.State = UserState.SignUpPhone;
                                await _client.SendTextMessageAsync(user.ChatID, "Мы рады что вы доверяете нам! Теперь осталось зарегистрироваться. Введите ваш номер телефона в интернациональном формате(пример: (+8618132341295): ");
                            }
                            else if (isNo)
                            {
                                _users.Remove(_users.Find(user => user.ChatID == message.From.Id)!);
                                databaseService.DeleteUser(user.ChatID);
                                await _client.SendTextMessageAsync(user.ChatID, "Мы уважаем ваше решение! Ваш аккаунт будет удалён сразу же после этого сообщения.");
                            }

                            return;
                        }

                        #endregion

                        #region SignUpPhone

                        if (user.State == UserState.SignUpPhone)
                        {
                            if (!message.Text.Contains("+"))
                            {
                                await _client.SendTextMessageAsync(user.ChatID, "Неправильный формат!");
                            }
                            else
                            {
                                user.Phone = message.Text;
                                user.State = UserState.SignUpApiID;

                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Отлично! Теперь вам нужно создать приложение на официальном сайте телеграма. Вот видео на ютубе как это сделать: https://youtu.be/JBDnmEhvgac . Если вам нужно помощь, напишите @uaquax_none");
                                await _client.SendTextMessageAsync(user.ChatID, "После того как создали приложение введите API_ID: ");
                            }
                            return;
                        }

                        #endregion

                        #region SignUpApiID

                        if (user.State == UserState.SignUpApiID)
                        {
                            try
                            {
                                user.ApiID = int.Parse(message.Text);
                                user.State = UserState.SignUpApiHash;
                            }
                            catch
                            {
                                await _client.SendTextMessageAsync(user.ChatID, "Неправильный API_ID! Попробуйте снова...");
                                return;
                            }
                            databaseService.UpdateUser(user);
                            
                            await _client.SendTextMessageAsync(user.ChatID, "Введите API_HASH: ");

                            return;
                        }

                        #endregion

                        #region SignUpApiHash

                        if (user.State == UserState.SignUpApiHash)
                        {
                            user.ApiHash = message.Text;
                            user.State = UserState.SignUpPassword;

                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, "Важно! Если у вас нет двухфакторной аутентификации, и соответсвенно пароля, просто напишите любое сообщение. \n\nВведите ваш пароль: ");

                            return;
                        }

                        #endregion

                        #region SignUpPassword

                        if (user.State == UserState.SignUpPassword)
                        {
                            user.Password = message.Text;
                            user.State = UserState.SignInTelegramService;

                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, "Регистрация завершена! Приятного использования ботом! Подключите свой аккаунт к боту чтобы запустить его /connect \n Если вы хотите начать заново, введите /reset");

                            return;
                        }

                        #endregion

                        #region MessageText

                        if (user.State == UserState.MessageText)
                        {
                            user.Messages.Add(new Message()
                            {
                                Text = message.Text
                            });
                            user.State = UserState.MessageTarget;
                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, "Кому вы хотите отправить это сообщение? Введите никнейм(пример: @uTgAutoBot), пока недоступны другие варианты: ");
                            return;
                        }

                        #endregion

                        #region MessageTarget

                        if (user.State == UserState.MessageTarget)
                        {
                            if (!message.Text.Contains("@"))
                            {
                                await _client.SendTextMessageAsync(user.ChatID, "Неверный формат! У никнейма должен быть символ @(пример: @uTgAutoBot)");
                                return;
                            }
                            user.Messages.LastOrDefault()!.Target = message.Text;
                            user.State = UserState.MessageIsWait;
                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, "Отлично! Вы хотите чтобы бот ожидал ответа прежде чем отправить следующее сообщение? да/нет");
                            return;
                        }

                        #endregion

                        #region MessageIsWait

                        if (user.State == UserState.MessageIsWait)
                        {
                            var isYes = TelegramService.yesList.Any(yesWord => message.Text.ToLower().Contains(yesWord));
                            var isNo = TelegramService.noList.Any(noWord => message.Text.Contains(noWord));

                            if (isYes == true && isNo == false)
                            {
                                user.Messages.LastOrDefault()!.IsWait = true;
                                user.State = UserState.MessageTargets;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Пример 1(Бот реагирует на сообщения содержащие слова привет, ку,хай: привет,ку,хай\nПример 2(Бот реагирует на сообщения содержащие цифры): @int\nПример 3(Бот реагирует на ЛЮБЫЕ сообщения): @string\nПример 4(Бот реагирует на сообщения содержащие ССЫЛКУ или НИКНЕЙМ): @link\nПример 5(Бот реагирует на сообщения содержащие утверждение(да, конечно, 100%, естественно, и тп. ): @yes\nПример 6(Бот реагирует на сообщения содержащие отрицание(нет, неа, и тп.)): @no\n\nВажно! \nЕсли вы хотите отвечать на любое сообщение, то введите @string \nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие цифры, то введите @int\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие ссылку(или телеграм аккаунт), то введите @link\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие утверждение(да), то введите @yes\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие отрицание(нет), то введите @no\n\nВведите текст на который будет отвечать бот(разделяя через запятую БЕЗ ПРОБЕЛА): ");

                                return;
                            }
                            else if (isNo == true && isYes == false)
                            {
                                user.Messages.LastOrDefault()!.IsWait = false;
                                user.State = UserState.MessageSleepTime;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, "Сколько времени вы хотите подождать перед отправлением следующего сообщения? (по-умолчанию: 1 сек.) Введите время ожидания в СЕКУНДАХ(пример: 50):");
                                return;
                            }
                            else
                            {
                                await _client.SendTextMessageAsync(user.ChatID, "Неверный формат!");
                                return;
                            }
                        }

                        #endregion

                        #region MessageTargets

                        if (user.State == UserState.MessageTargets)
                        {
                            try
                            {
                                if (message.Text!.Contains(","))
                                    user.Messages.LastOrDefault()!.Targets = message.Text!.Replace(" ", "").Split(",").ToList();
                                else
                                    user.Messages.LastOrDefault()!.Targets = new List<string>() { message.Text };
                                user.State = UserState.MessageAnswer;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения содержщие: {message.Text}\n\n Важно! Если вы хотите чтобы на сообщение ответила нейронная сеть, то введите @ai.\n\nЕсли вы хотите чтобы после получения данного сообщения бот начал отправлять сообщения заново с первого сообщения, то введите: @restart\n\nВведите текст который бот будет отправлять при получении сообщения содержащее указаные вами символы: ");
                            }
                            catch
                            {
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так... Попробуйте заново");
                            }
                            return;
                        }

                        #endregion

                        #region MesssageAnswer

                        if (user.State == UserState.MessageAnswer)
                        {
                            user.Messages.LastOrDefault()!.Answer = message.Text;
                            user.State = UserState.MessageAskAI;
                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения содержщие указаные вами символы этим текстом: \n\nВы хотите чтобы бот отвечал на полученное сообщение с помощью нейронной сети?(да/нет)");
                            return;
                        }

                        #endregion

                        #region MessageAskAI

                        if (user.State == UserState.MessageAskAI)
                        {
                            var isYes = TelegramService.yesList.Any(yesWord => message.Text.ToLower().Contains(yesWord));
                            var isNo = TelegramService.noList.Any(noWord => message.Text.Contains(noWord));

                            if (isYes)
                            {
                                user.Messages.LastOrDefault()!.AskAI = true;
                                user.State = UserState.MessageInformation;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Пример: \n\nКомпания: Google\nТелефоне: +4614352345\nАдрес компании: Улица Шанпиньона 54\n...\n\nВведите информацию на основе которой нейронная сеть будет отвечать: ");
                            }
                            else
                            {
                                user.Messages.LastOrDefault()!.Information = message.Text;
                                user.State = UserState.MessageSleepTime;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"\n\nСколько времени вы хотите подождать перед отправлением следующего сообщения? (по-умолчанию: 1 сек.) Введите время ожидания в СЕКУНДАХ(пример: 50):");
                            }
                            return;
                        }

                        #endregion

                        #region MessageInformation

                        if (user.State == UserState.MessageInformation)
                        {
                            user.Messages.LastOrDefault()!.Information = message.Text;
                            user.State = UserState.MessageSleepTime;
                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, $"Готово! Нейронная сеть будет отвечать на основе этой информации:\n{user.Messages.LastOrDefault()!.Information}");
                            await _client.SendTextMessageAsync(user.ChatID, $"\n\nСколько времени вы хотите подождать перед отправлением следующего сообщения? (по-умолчанию: 1 сек.) Введите время ожидания в СЕКУНДАХ(пример: 50):");
                            return;
                        }

                        #endregion

                        #region MesssageSleepTime

                        if (user.State == UserState.MessageSleepTime)
                        {
                            try
                            {
                                user.Messages.LastOrDefault()!.SleepTime = TimeSpan.FromSeconds(new Random().Next(int.Parse(message.Text), int.Parse(message.Text)+1));
                                user.State = UserState.MessagesConfirm;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Ваше сообщение готово! Вот как выглядят её настройки: \n\n{JsonConvert.SerializeObject(user.Messages)}\n\n Чтобы начать заново введите /reset_messages \n\nВы хотите добавить ещё одно сообщение? (да/нет)");
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так!");
                            }
                            return;
                        }

                        #endregion

                        #region MessagesConfirm

                        if (user.State == UserState.MessagesConfirm)
                        {
                            try
                            {
                                var isYes = TelegramService.yesList.Any(yesWord => message.Text.ToLower().Contains(yesWord));
                                var isNo = TelegramService.noList.Any(noWord => message.Text.Contains(noWord));

                                if (isYes)
                                {
                                    user.State = UserState.MessagesConfirm;
                                    databaseService.UpdateUser(user);

                                    await _client.SendTextMessageAsync(user.ChatID, $"Поздравляю ваше сообщение успешно создано! Вот как выглядят настройки вашего сообщения:\n\n{JsonConvert.SerializeObject(user.Messages)}\n\n");
                                    await _client.SendTextMessageAsync(user.ChatID, "Укажите текст который ваш бот будет отправлять. Например: Привет!");
                                    return;
                                }
                                else if (isNo)
                                {
                                    user.State = UserState.ParallelMessageConfirm;
                                    await _client.SendTextMessageAsync(user.ChatID, "Вы хотите отвечать на конкретные сообщения вне очереди(паралельно отправке сообщение)? (пока сообщения будут отправляться, собеседник может задать вам вопрос, на который вы захотите ответить. Для этого нужны паралельные сообщения). \n\nК примеру, собеседник спросит ваше имя, а вы назовете его не прекращая цикл.\n\nИли если Когда человек пишет вам 'До свидания' вы хотите начинать цикл заново, для этого тоже нужны паралельные сообщения. \n\n(да/нет):");

                                    databaseService.UpdateUser(user);
                                    return;
                                }

                                return;
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так!");
                            }
                            return;
                        }

                        #endregion

                        #region ParallelMessageTargets

                        if (user.State == UserState.ParallelMessageTargets)
                        {
                            try
                            {
                                if (message.Text!.Contains(","))
                                    user.ParallelMessages.Add(new ParallelMessage()
                                    {
                                        Targets = message.Text!.Replace(" ", "").Split(",").ToList()
                                    });
                                else
                                    user.ParallelMessages.Add(new ParallelMessage()
                                    {
                                        Targets = new List<string>() { message.Text }
                                    });
                                user.State = UserState.ParallelMessageAnswer;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения содержщие: {message.Text}\n\n Важно! Если вы хотите чтобы на сообщение ответила нейронная сеть, то введите @ai.\n\nЕсли вы хотите чтобы после получения данного сообщения бот начал отправлять сообщения заново с первого сообщения, то введите: @restart\n\nВведите текст который бот будет отправлять при получении сообщения содержащее указаные вами символы: ");
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так!");
                            }
                            return;
                        }

                        #endregion


                        #region ParallelMessageAnswer

                        if (user.State == UserState.ParallelMessageAnswer)
                        {
                            try
                            {
                                user.ParallelMessages.LastOrDefault()!.Answer = message.Text;
                                user.State = UserState.ParallelMessageTarget;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения содержщие указаные вами символы этим текстом: {message.Text}");
                                await _client.SendTextMessageAsync(user.ChatID, "Чьи сообщения вас интересуют? Введите никнейм(пример: @uTgAutoBot), другие варианты пока недоступны: ");

                                return;
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так!");
                            }
                            return;
                        }

                        #endregion

                        #region ParallelMessageTarget

                        if (user.State == UserState.ParallelMessageTarget)
                        {
                            try
                            {
                                user.State = UserState.ParallelMessageAskAI;
                                user.ParallelMessages.LastOrDefault()!.Target = message.Text;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения от {message.Text} содержщие указаные вами символы этим текстом: {user.ParallelMessages.LastOrDefault()!.Answer}\n\nВы хотите чтобы бот отвечал на полученное сообщение с помощью нейронной сети?(да/нет)");
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так!");
                            }
                            return;
                        }

                        #endregion

                        #region ParallelMessageAskAI

                        if (user.State == UserState.ParallelMessageAskAI)
                        {
                            var isYes = TelegramService.yesList.Any(yesWord => message.Text.ToLower().Contains(yesWord));
                            var isNo = TelegramService.noList.Any(noWord => message.Text.Contains(noWord));

                            if (isYes)
                            {
                                user.ParallelMessages.LastOrDefault()!.AskAI = true;
                                user.State = UserState.ParallelMessageInformation;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Пример: \n\nКомпания: Google\nТелефоне: +4614352345\nАдрес компании: Улица Шанпиньона 54\n...\n\nВведите информацию на основе которой нейронная сеть будет отвечать: ");
                            }
                            else
                            {
                                user.ParallelMessages.LastOrDefault()!.Information = message.Text;
                                user.State = UserState.ParallelMessageConfirm;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Вы хотите добавить ещё одно паралельное сообщение? (да/нет)");
                            }
                            return;
                        }

                        #endregion

                        #region ParallelMessageInformation

                        if (user.State == UserState.ParallelMessageInformation)
                        {
                            user.ParallelMessages.LastOrDefault()!.Information = message.Text;
                            user.State = UserState.ParallelMessageConfirm;
                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, $"Готово! Нейронная сеть будет отвечать на основе этой информации:\n{user.Messages.LastOrDefault()!.Information}");
                            await _client.SendTextMessageAsync(user.ChatID, $"Вы хотите добавить ещё одно паралельное сообщение? (да/нет)");
                            
                            return;
                        }

                        #endregion

                        #region ParallelMessageConfirm

                        if (user.State == UserState.ParallelMessageConfirm)
                        {
                            try
                            {
                                var isYes = TelegramService.yesList.Any(yesWord => message.Text.ToLower().Contains(yesWord));
                                var isNo = TelegramService.noList.Any(noWord => message.Text.Contains(noWord));

                                if (isYes)
                                {
                                    user.State = UserState.ParallelMessageTargets;
                                    databaseService.UpdateUser(user);

                                    await _client.SendTextMessageAsync(user.ChatID, "Пример 1(Бот реагирует на сообщения содержащие слова привет, ку,хай: привет,ку,хай\nПример 2(Бот реагирует на сообщения содержащие цифры): @int\nПример 3(Бот реагирует на ЛЮБЫЕ сообщения): @string\nПример 4(Бот реагирует на сообщения содержащие ССЫЛКУ или НИКНЕЙМ): @link\nПример 5(Бот реагирует на сообщения содержащие утверждение(да, конечно, 100%, естественно, и тп. ): @yes\nПример 6(Бот реагирует на сообщения содержащие отрицание(нет, неа, и тп.)): @no\n\nВажно! \nЕсли вы хотите отвечать на любое сообщение, то введите @string \nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие цифры, то введите @int\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие ссылку(или телеграм аккаунт), то введите @link\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие утверждение(да), то введите @yes\nЕсли вы хотите чтобы бот отвечал на любые сообщения содержащие отрицание(нет), то введите @no\n\nВведите текст на который будет отвечать бот(разделяя через запятую БЕЗ ПРОБЕЛА): ");
                                }
                                else if (isNo)
                                {
                                    user.State = UserState.Ready;
                                    user.TelegramService = new TelegramService(user.Messages, user.ParallelMessages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID, user.Coins);
                                    var result = user.TelegramService.Connect().Result;

                                    if (result != null && result.Message!.Contains("FLOOD"))
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                        return;
                                    }
                                    else if (result != null && result.IsEnoughCoins == false)
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"У вас недостаточно монет({user.Coins}).");

                                        user.State = UserState.Start;
                                        user.TelegramService!.Disconnect();
                                        user.TelegramService = null;
                                        databaseService.UpdateUser(user);

                                        await _client.SendTextMessageAsync(user.ChatID, $"Отключаюсь! /connect чтобы заново подключиться!");
                                        return;
                                    }

                                    if (user.TelegramService.IsConnected)
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, "Вы успешно подключили ваш аккаунт к боту. Теперь напишите /start чтобы запустить его!");
                                    }
                                    else
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так... Попробуйте подключиться заново /conect");
                                    }
                                    databaseService.UpdateUser(user);
                                }
                                else if (isNo && user.TelegramService != null && user.TelegramService.IsConnected)
                                {
                                    await _client.SendTextMessageAsync(user.ChatID, $"Успешно! ");
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
                                await _client.SendTextMessageAsync(user.ChatID, "Что-то пошло не так!");
                            }
                            return;
                        }

                        #endregion

                        if (user == null)
                        {
                            await _client.SendTextMessageAsync(message.From.Id, "Прежде чем пользовать ботом, вам нужно зарегистрировать! Введите /connect чтобы подключить свой аккаунт к боту\n Введите /start чтобы начать регистрацию, или /reset чтбы удалить аккаунт из базы.");
                            return;
                        }

                        break;
                    case UpdateType.CallbackQuery:
                        var callback = update.CallbackQuery;
                        var callback_user = _users.Find(u => u.ChatID == callback!.From.Id);

                        if (callback_user!.State == UserState.SignUpVerify)
                        {
                            var isYes = callback!.Data!.Contains("yes");
                            var isNo = callback!.Data!.Contains("no");

                            if (isYes)
                            {
                                callback_user.State = UserState.SignUpPhone;
                                await _client.SendTextMessageAsync(callback_user.ChatID, "Мы рады что вы доверяете нам! Теперь осталось зарегистрироваться. Введите ваш номер телефона в интернациональном формате(пример: (+8618132341295): ");
                            }
                            else if (isNo)
                            {
                                _users.Remove(_users.Find(user => user.ChatID == callback.From.Id)!);
                                databaseService.DeleteUser(callback_user.ChatID);
                                await _client.SendTextMessageAsync(callback_user.ChatID, "Мы уважаем ваше решение! Ваш аккаунт будет удалён сразу же после этого сообщения.");
                            }

                            await _client.DeleteMessageAsync(callback.From.Id, callback.Message!.MessageId);

                            return;
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Error($"BotService.handleUpdateAsync: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }
    }
}
