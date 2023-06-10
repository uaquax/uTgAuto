using dotenv.net;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
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
                    LoggerService.Info($"Start listening for @{me.Username} \nInformation: {Thread.CurrentThread.ManagedThreadId}\t{Thread.CurrentThread.Name}");
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
                catch
                {
                    LoggerService.Info($"Start listening for @{me.Username}");
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
                                bool isSignedUp = _users.Find(u => u.ChatID == message.From!.Id) != null;

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
                                                databaseService.UpdateUser(user);
                                            }
                                            else
                                            {
                                                await _client.SendTextMessageAsync(user.ChatID, "Ваш код получен! /start чтобы запустить бота");
                                            
                                                 user.State = UserState.Ready;
                                    user.TelegramService = new TelegramService(user.Messages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID);
                                    var result = user.TelegramService.Connect().Result;
                                    
                                    if (result != null && result.Message!.Contains("FLOOD"))
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
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

                                if (!isSignedUp)
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
                                                chatIdOwner.Credits++;
                                                databaseService.UpdateUser(chatIdOwner);
                                                await _client.SendTextMessageAsync(chatId, $"По вашей реферальной ссылке присоединился 1 пользователь. За это вы получаете 1 монету!\n\nВведите /credits чтобы посмотреть ваш баланс.");
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
                                        user = new User()
                                        {
                                            ChatID = message.From.Id,
                                            State = UserState.SignUpVerify
                                        };
                                        _users.Add(user);
                                        databaseService.AddUser(user);
                                        await _client.SendTextMessageAsync(user.ChatID, $"Добро Пожаловать, {message.From.FirstName} {message.From.LastName}! Это бот для автоматического общения интегрированный с ИИ! \n\nЧтобы начать использование вам нужно для начала зарегистрироваться, не бойтесь, это не сложно.\n\nНаш бот использует Telegram API для подключения к вашему аккаунту и отправке, чтению сообщений в чате указаным ВАМИ. \nНаш бот не собирает данные о нашим пользователях.\n\nЧто наш бот делает: * Подключается к аккаунта\n* Отправляет сообщения указанные вами в указанный чат\n* Читает сообщения если вы это указали в указанном вами чате\nПри желании, использует ИИ для ответа на вопрос исходя из указанной вами информации.\n\nЧто наш бот НЕ делает:\n* НЕ собирает данные об отправленных или полученных сообщениях. \nНЕ делает действий не указаных ВАМИ на вашем аккаунте\n\nЕсли вы согласны с тем что наш бот подключится к вашему аккаунту напишите Да, если не согласны напишите Нет, и мы моментально удалим всю существующую о вас инфомрацию из нашей базы данных. ");

                                        return;
                                    }
                                }
                                if (user!.Credits > 0 && user!.Messages != null && user!.Messages.Count != 0)
                                {
                                    user.State = UserState.Running;
                                    databaseService.UpdateUser(user);

                                    await _client.SendTextMessageAsync(message.From!.Id, $"Запускаю! Доступно монет: {user.Credits}");

                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            var result = user.TelegramService!.Start().Result;

                                            if (result != null && result.Message!.Contains("FLOOD"))
                                            {
                                                await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                            }
                                        }
                                        catch { }
                                    });

                                    await _client.SendTextMessageAsync(message.From.Id, "Запустил!");
                                }
                                else if (user!.Credits <= 0)
                                {
                                    await _client.SendTextMessageAsync(message.From!.Id, $"Недостаточно монет({user.Credits})! Пригласи друга по реферальной ссылке или купи новые монеты.");
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
                                    user.TelegramService = new TelegramService(user.Messages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID);
                                    var result = user.TelegramService.Connect().Result;
                                    if (result != null && result.Message!.Contains("FLOOD"))
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                                    {
                                        new[]
                                        {
                                            InlineKeyboardButton.WithUrl("Ввести код", $"127.0.0.1:8080?chat_id={user.ChatID}")
                                        }
                                     });

                                    await _client.SendTextMessageAsync(user.ChatID, $"Введите код полученный от Телеграма(это нужно чтобы подключиться к аккаунту): ", replyMarkup: inlineKeyboard);
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
                                if (user!.State != UserState.SignInTelegramService)
                                {
                                    user.State = UserState.Start;
                                    user.TelegramService!.Disconnect();
                                    user.TelegramService = null;
                                    databaseService.UpdateUser(user);

                                    await _client.SendTextMessageAsync(user.ChatID, $"Отключаюсь! /connect чтобы заново подключиться!");
                                }
                                else
                                {
                                    await _client.SendTextMessageAsync(user.ChatID, $"Вы не подключены!");
                                }
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

                        #region /credits

                        if (message.Text.Contains("/credits"))
                        {
                            await _client.SendTextMessageAsync(user!.ChatID, $"Ваш баланс: {user.Credits}");
                        }

                        #endregion

                        #region /new_message

                        if (message.Text.Contains("/new_message"))
                        {
                            try
                            {
                                user!.State = UserState.MessageText;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Поздравляю ваше сообщение успешно создано! Вот как выглядят настройки вашего сообщения:```\n{JsonConvert.SerializeObject(user.Messages)}```");
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

                        #region SignInTelegramService

                        if (user!.State == UserState.SignInTelegramService)
                        {
                            try
                            {
                                File.Delete($"Sessions/{user.ChatID}.session");
                            }
                            catch { }

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    user.State = UserState.Ready;
                                    user.TelegramService = new TelegramService(user.Messages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID);
                                    var result = user.TelegramService.Connect().Result;

                                    if (result != null && result.Message!.Contains("FLOOD"))
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
                                    }

                                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                                    {
                                    new[]
                                    {
                                        InlineKeyboardButton.WithUrl("Ввести код", $"http://127.0.0.1:8080?chat_id={user.ChatID}")
                                    }
                                });

                                    await _client.SendTextMessageAsync(user.ChatID, $"Введите код полученный от Телеграма(это нужно чтобы подключиться к аккаунту): ", replyMarkup: inlineKeyboard);
                                    await _client.SendTextMessageAsync(user.ChatID, $"После того как ввели ваш код, напишите /connect заново чтобы использовать его!", replyMarkup: inlineKeyboard);
                                }
                                catch { }
                            });

                            return;
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

                                await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения содержщие: {message.Text}\n\n Введите текст который бот будет отправлять при получении сообщения содержащее указаные вами символы: ");
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
                            user.State = UserState.MessageSleepTime;
                            databaseService.UpdateUser(user);

                            await _client.SendTextMessageAsync(user.ChatID, $"Бот будет отвечать на все сообщения содержщие указаные вами символы этим текстом: \n\nСколько времени вы хотите подождать перед отправлением следующего сообщения? (по-умолчанию: 1 сек.) Введите время ожидания в СЕКУНДАХ(пример: 50):");
                            return;
                        }

                        #endregion

                        #region MessageAskAI

                        if (user.State == UserState.MessageAskAI)
                        {

                        }

                        #endregion

                        #region MesssageSleepTime

                        if (user.State == UserState.MessageSleepTime)
                        {
                            try
                            {
                                user.Messages.LastOrDefault()!.SleepTime = TimeSpan.FromSeconds(double.Parse(message.Text));
                                user.State = UserState.MessagesConfirm;
                                databaseService.UpdateUser(user);

                                await _client.SendTextMessageAsync(user.ChatID, $"Ваше сообщение готово! Вот как выглядят её настройки: ```\n{JsonConvert.SerializeObject(user.Messages)}```\n\n Чтобы начать заново введите /reset_messages \n\nВы хотите добавить ещё одно сообщение? (да/нет)");
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
                                    user.State = UserState.MessageText;
                                    databaseService.UpdateUser(user);

                                    await _client.SendTextMessageAsync(user.ChatID, $"Поздравляю ваше сообщение успешно создано! Вот как выглядят настройки вашего сообщения:```\n{JsonConvert.SerializeObject(user.Messages)}```\n\n Введите ");
                                    await _client.SendTextMessageAsync(user.ChatID, "Укажите текст который ваш бот будет отправлять. Например: Привет!");
                                }
                                else if (isNo)
                                {
                                    user.State = UserState.Ready;
                                    user.TelegramService = new TelegramService(user.Messages, user.ApiID.ToString(), user.ApiHash, user.Phone, user.Password, user.ChatID);
                                    var result = user.TelegramService.Connect().Result;
                                    
                                    if (result != null && result.Message!.Contains("FLOOD"))
                                    {
                                        await _client.SendTextMessageAsync(user.ChatID, $"Вас временно заблокировал Телеграм за флуд, сообщение ошибки: {result.Message}. Вам стоит подождать столько минут сколько указано после FLOOD_WAIT_, если там X, то подождите 5 минут, в других случаях столько сколько указано. Не бойтесь, это нормально, возможно вам стоит сделать ожидание между сообщениями больше, чтобы таокго не повторялось.");
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
                                    await _client.SendTextMessageAsync(user.ChatID, $"Поздравляю ваши сообщения готовы! Вот как выглядят настройки ваших сообщений:```\n{JsonConvert.SerializeObject(user.Messages)}```\n\n Чтобы начать заново введите /reset_messages \n\nТеперь вы можете запустить бота командой /start. Приятного использования! \nЧтобы создать новые сообщения напишите комманду: /resetMessages");
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
