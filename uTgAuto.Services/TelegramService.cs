using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using TL;
using uTgAuto.Services.Models;
using WTelegram;
using Message = uTgAuto.Services.Models.Message;
using UserTL = TL.User;


namespace uTgAuto.Services
{
    enum State
    {
        Work,
        Wait
    }
    public class TelegramService
    {

        public static readonly List<string> yesList = new List<string>()
        {
            "yes", "yep", "yeah", "sure", "absolutely", "certainly",
            "да", "угу", "верно", "точно", "конечно", "естественно", "ага",
            "так", "такий", "впевнено", "впевнено", "звичайно", "точно"
        };
        public static readonly List<string> noList = new List<string>()
        {
            "no", "nope", "nah", "not at all", "certainly not", "definitely not",
            "нет", "не", "ни", "нисколько", "вовсе нет", "абсолютно нет",
            "ні", "не", "вже ні", "зовсім не", "точно ні", "абсолютно ні"
        };

        public bool IsConnected { get; set; } = false;

        private bool _isInChat = false;
        private State _state = State.Work;
        private Client? _client;
        private static UserTL? _user;
        private Message? _waitingMessage;
        private List<Message> _messages = new List<Message>();
        private List<ParallelMessage> _parallelMessages = new List<ParallelMessage>();
        private readonly Dictionary<long, UserTL> _users = new();
        private readonly Dictionary<long, ChatBase> _chats = new();
        private readonly Auth? _auth;
        private bool _isRunning = false;
        private int _messageIndex = 0;
        private Flood? _flood;

        public TelegramService(List<Message> messages, List<ParallelMessage> parallelWaitingMessages, string apiId, string apiHash, string phoneNumber, string password, long chatID)
        {
            try
            {
                //#if DEBUG
                //                LoggerService.Debug("Debug Version");
                //#else
                //            Helpers.Log = (lvl, str) => { };
                //#endif
                Helpers.Log = (lvl, str) => { };
                _messages = messages;
                _parallelMessages = parallelWaitingMessages;
                _auth = new Auth(int.Parse(apiId), apiHash, phoneNumber, password, chatID);

                if (apiId == null || apiHash == null || phoneNumber == null)
                {
                    LoggerService.Error($"Couldn't connect to. API_ID / API_HASH / PHONE_NUMBER is null. Please specify it in the .env file");
                    return;
                }

                LoggerService.Debug($"\nCHAT ID: {chatID}\nAPI ID: {_auth!.ApiId}\nAPI HASH: {_auth.ApiHash}\nPHONE NUMBER: {_auth.PhoneNumber}");
            }
            catch { }
        }
        ~TelegramService()
        {
            try
            {
                IsConnected = false;
                _client!.Dispose();
                _client = null;
                _user = null;
            }
            catch { }
        }

        public void Stop()
        {
            try
            {
                _isRunning = false;
            }
            catch { }
        }

        public void Disconnect()
        {
            try
            {
                _isRunning = false;
                IsConnected = false;
                _client!.Dispose();
                _client = null;
                _user = null;
            }
            catch { }
        }

        public async Task<Flood?> Start()
        {
            try
            {
                try
                {
                    LoggerService.Debug($"Telegram.Service.Start(): Start is called. \nInformation: {_user!.id}\t{Thread.CurrentThread.ManagedThreadId}\t{Thread.CurrentThread.Name}");
                }
                catch
                {
                    LoggerService.Debug($"Telegram.Service.Start(): Start is called.");

                }

                if (_client == null) return null;
                _isRunning = true;

                while (_isRunning)
                {
                    try
                    {
                        LoggerService.Debug($"Telegram.Service.Start()_while(_isRunning): While loop. \nInformation: {_user!.id}\t{_isRunning}\t{_state}\t{_messageIndex}");
                    }
                    catch
                    {
                        LoggerService.Debug($"Telegram.Service.Start()_while(_isRunning): While loop.");
                    }

                    try
                    {
                        if (_state == State.Wait) continue;

                        while (_messageIndex < _messages.Count())
                        {
                            var message = _messages[_messageIndex];

                            if (message == null || _state != State.Work) continue;

                            if (message.Text != null && message.Text != string.Empty)
                            {
                                var resolved = await _client.Contacts_ResolveUsername(message.Target!.Replace("@", string.Empty));
                                await _client!.SendMessageAsync(resolved, message.Text);
                            }

                            _messageIndex++;

                            if (_messageIndex == _messages.Count())
                                _messageIndex = 0;

                            if (message.Targets != null
                                && _waitingMessage == null
                                && message.IsWait == true)
                            {
                                _state = State.Wait;
                                _waitingMessage = message;
                                continue;
                            }
                            try
                            {
                                LoggerService.Debug($"Telegram.Service.Start()_TaskDelay({message.SleepTime}): \nInformation: {_user!.id}\t{Thread.CurrentThread.ManagedThreadId}\t{Thread.CurrentThread.Name}");
                            }
                            catch (Exception)
                            {
                                LoggerService.Debug($"Telegram.Service.Start()_TaskDelay({message.SleepTime})");
                            }
                            await Task.Delay(message.SleepTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Warning($"[{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");

                        if (ex.Message.Contains("FLOOD_WAIT"))
                        {
                            LoggerService.Warning($"FLOOD ERROR. Wait for: {ex.Message.Split("FLOOD_WAIT_")[1].Split("_")[0]}");

                            _isRunning = false;
                            _flood = new Flood() { Message = ex.Message };
                            return _flood;
                        }
                    }

                    await Task.Delay(750);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("FLOOD"))
                {
                    LoggerService.Warning($"FLOOD ERROR. Wait for: {ex.Message.Split("FLOOD_WAIT_")[1].Split("_")[0]}");
                    _flood = new Flood() { Message = ex.Message };
                    return _flood;
                }

                LoggerService.Error($"Telegram.Service.Start: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }

            return _flood;
        }

        public async Task<Flood?> Connect()
        {
            try
            {
                try
                {
                    LoggerService.Debug($"Telegram.Service.Connect(): Connect is called. \nInformation: {_user!.id}\t{Thread.CurrentThread.ManagedThreadId}\t{Thread.CurrentThread.Name}");
                }
                catch
                {
                    LoggerService.Debug($"Telegram.Service.Connect(): Connect is called.");
                }

                Console.ForegroundColor = ConsoleColor.Gray;
                LoggerService.Debug("Connecting to Telegram...");
                Console.ResetColor();

                _client = new Client(_auth!.Config);

                _user = await _client.LoginUserIfNeeded();
                _users[_user.id] = _user;

                Console.ForegroundColor = ConsoleColor.Green;
                LoggerService.Debug($"Connected as {_user.first_name} {_user.last_name} +{_user.phone}");
                Console.ResetColor();

                IsConnected = true;

                var dialogs = await _client.Messages_GetAllDialogs();
                dialogs.CollectUsersChats(_users, _chats);

                _client.OnUpdate += onUpdate;

            }
            catch (Exception ex)
            {
                try
                {
                    
                    if (ex.Message.Contains("FLOOD"))
                    {
                        LoggerService.Warning($"FLOOD ERROR. Wait for: {ex.Message.Split("FLOOD_WAIT_")[1].Split("_")[0]}");
                    }
                    if (ex.Message.Contains("PHONE") == false)
                        LoggerService.Error($"Telegram.Service.Connect: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");

                    IsConnected = false;
                    _client!.Dispose();
                    _client = null;
                    _user = null; 
                    
                    _flood = new Flood() { Message = ex.Message };
                    return _flood;
                }
                catch { }
            }

            return _flood;
        }

        private async Task onUpdate(IObject arg)
        {
            try
            {
                if (arg is not UpdatesBase updates || _state == State.Work) return;

                foreach (var update in updates.UpdateList)
                {
                    switch (update)
                    {
                        case UpdateNewMessage unm:
                            await displayMessage(unm.message);
                            break;
                        default:
                            break;
                    }
                }
            }

            catch (Exception ex)
            {
                if (ex.Message.Contains("FLOOD"))
                {
                    LoggerService.Warning($"FLOOD ERROR. Wait for: {ex.Message.Split("FLOOD_WAIT_")[1].Split("_")[0]}");
                }

                LoggerService.Error($"Telegram.Service.onUpdate: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        private async Task displayMessage(MessageBase messageBase)
        {
            try
            {
                switch (messageBase)
                {
                    case TL.Message message:
                        try
                        {
                            var resolved = await _client.Contacts_ResolveUsername(_messages[_messageIndex]!.Target!.Replace("@", string.Empty));
                            if (Peer(message.peer_id) != Peer(resolved.peer)) return;
                        }
                        catch { }

                        LoggerService.Trace($"TelegramSerivce.displayMessage(): case TL.Message {message.from_id} -> {message.message} \n\t-> {_waitingMessage}");

                        if (_parallelMessages != null)
                        {
                            foreach (var pMessage in _parallelMessages)
                            {
                                var targets = pMessage?.Targets;
                                var isInt = targets?.Find(target => "@int".Contains(target.ToLower())) == null ? false : true;
                                var isString = targets?.Find(target => "@string".Contains(target.ToLower())) == null ? false : true;
                                var isLink = targets?.Find(target => "@link".Contains(target.ToLower())) == null ? false : true;
                                var isYes = targets?.Find(target => "@yes".Contains(target.ToLower())) == null ? false : true;
                                var isNo = targets?.Find(target => "@no".Contains(target.ToLower())) == null ? false : true;
                                var isRestart = targets?.Find(targets => "@restart".Contains(targets.ToLower())) == null ? false : true;

                                var resolved = await _client.Contacts_ResolveUsername(_waitingMessage?.Target?.Replace("@", string.Empty));

                                bool isContains = false;

                                if (isRestart)
                                {
                                    if (Peer(message.peer_id) != Peer(resolved.peer)) return;

                                    _state = State.Work;
                                    _waitingMessage = null;
                                    _messageIndex = 0;
                                    _isInChat = false;
                                    return;
                                }

                                if (isInt == false
                                    && isString == false
                                    && isLink == false
                                    && isYes == false
                                    && isNo == false
                                    && isRestart == false)
                                {
                                    isContains = targets?.Find(target => message.message.ToLower().Contains(target.ToLower())) == null ? false : true;
                                }
                                else
                                {
                                    if (isInt)
                                    {
                                        isContains = Regex.IsMatch(message.message, @"\d");
                                    }
                                    else if (isString)
                                    {
                                        isContains = true;
                                    }
                                    else if (isLink)
                                    {
                                        isContains = message.message.ToLower().Contains("http") || message.message.ToLower().Contains("t.me") || message.message.ToLower().Contains("@") || message.message.ToLower().Contains("@");
                                    }
                                    else if (isYes)
                                    {
                                        isContains = yesList.Any(yesWord => message.message.Contains(yesWord));
                                    }
                                    else if (isNo)
                                    {
                                        isContains = noList.Any(noWord => message.message.Contains(noWord));
                                    }
                                }
                                if (isContains && pMessage!.Answer != "@none")
                                {
                                    if (_messageIndex == 1) _isInChat = true;
                                    else _isInChat = false;
                                    if (_waitingMessage!.Answer!.Contains("@ai"))
                                    {
                                        var aiAnswer = await AIService.Ask(_waitingMessage!.Information, message.message);
                                        await _client!.SendMessageAsync(resolved, aiAnswer, reply_to_msg_id: message.ID);
                                        _state = State.Work;
                                        _waitingMessage = null;
                                    }
                                    else
                                    {
                                        await _client!.SendMessageAsync(resolved, _waitingMessage?.Answer, reply_to_msg_id: message.ID);
                                        _state = State.Work;
                                        _waitingMessage = null;
                                    }
                                }
                            }
                        }

                        LoggerService.Trace($"TelegramSerivce.displayMessage(): _waitingMessage ( {_waitingMessage} ) != null && _waitingMessage.Target ( {_waitingMessage!.Target} ) != null");
                        
                        if (_waitingMessage != null
                            && _waitingMessage.Target != null)
                        {
                            var resolved = await _client.Contacts_ResolveUsername(_waitingMessage!.Target!.Replace("@", string.Empty));
                            if (Peer(message.peer_id) != Peer(resolved.peer)) return;
                        }
                        else
                        {
                            if (_messages[_messageIndex].AskAI)
                            {
                                var resolved = await _client.Contacts_ResolveUsername(_messages[_messageIndex]!.Target!.Replace("@", string.Empty));
                                if (Peer(message.peer_id) != Peer(resolved.peer)) return;

                                var aiAnswer = await AIService.Ask(_waitingMessage!.Information, message.message);

                                await _client!.SendMessageAsync(resolved, aiAnswer, reply_to_msg_id: message.ID);
                            }

                        }

                        LoggerService.Trace($"TelegramSerivce.displayMessage():if (_state( {_state} ) == State.Wait && message.peer_id.ID ( {message.Peer.ID} ) != _user?.id ( {_user?.id} ) )");
                        if (_state == State.Wait && message.peer_id.ID != _user?.id)
                        {
                            var targets = _waitingMessage?.Targets;
                            var isInt = targets?.Find(target => "@int".Contains(target.ToLower())) == null ? false : true;
                            var isString = targets?.Find(target => "@string".Contains(target.ToLower())) == null ? false : true;
                            var isLink = targets?.Find(target => "@link".Contains(target.ToLower())) == null ? false : true;
                            var isYes = targets?.Find(target => "@yes".Contains(target.ToLower())) == null ? false : true;
                            var isNo = targets?.Find(target => "@no".Contains(target.ToLower())) == null ? false : true;

                            var resolved = await _client.Contacts_ResolveUsername(_waitingMessage?.Target?.Replace("@", string.Empty));

                            bool isContains = false;

                            if (isInt == false
                                && isString == false
                                && isLink == false
                                && isYes == false
                                && isNo == false)
                            {
                                isContains = targets?.Find(target => message.message.ToLower().Contains(target.ToLower())) == null ? false : true;
                            }
                            else
                            {
                                if (isInt)
                                {
                                    isContains = Regex.IsMatch(message.message, @"\d");
                                }
                                else if (isString)
                                {
                                    isContains = true;
                                }
                                else if (isLink)
                                {
                                    isContains = message.message.ToLower().Contains("http") || message.message.ToLower().Contains("t.me") || message.message.ToLower().Contains("@") || message.message.ToLower().Contains("@");
                                }
                                else if (isYes)
                                {
                                    isContains = yesList.Any(yesWord => message.message.Contains(yesWord));
                                }
                                else if (isNo)
                                {
                                    isContains = noList.Any(noWord => message.message.Contains(noWord));
                                }
                            }
                            if (isContains && _waitingMessage!.Answer != "@none")
                            {
                                if (_messageIndex == 1) _isInChat = true;
                                else _isInChat = false;

                                if (_waitingMessage!.Answer!.Contains("@ai"))
                                {
                                    var aiAnswer = await AIService.Ask(_waitingMessage!.Information, message.message);
                                    await _client!.SendMessageAsync(resolved, aiAnswer, reply_to_msg_id: message.ID);
                                    _state = State.Work;
                                    _waitingMessage = null;
                                }
                                else
                                {
                                    await _client!.SendMessageAsync(resolved, _waitingMessage?.Answer, reply_to_msg_id: message.ID);
                                    _state = State.Work;
                                    _waitingMessage = null;
                                }
                            }
                            else
                            {
                                if (_messages[_messageIndex].AskAI)
                                {
                                    if (Peer(message.peer_id) != Peer(resolved.peer)) return;

                                    var aiAnswer = await AIService.Ask(_waitingMessage!.Information, message.message);

                                    await _client!.SendMessageAsync(resolved, aiAnswer, reply_to_msg_id: message.ID);
                                }
                            }
                        }
                        else
                        {
                            if (_messages[_messageIndex].AskAI)
                            {
                                var resolved = await _client.Contacts_ResolveUsername(_messages[_messageIndex]!.Target!.Replace("@", string.Empty));
                                if (Peer(message.peer_id) != Peer(resolved.peer)) return;

                                var aiAnswer = await AIService.Ask(_waitingMessage!.Information, message.message);
                                LoggerService.Trace($"TelegramService.displaymessage(): {aiAnswer} Sent via AI");
                                await _client!.SendMessageAsync(resolved, aiAnswer, reply_to_msg_id: message.ID);
                            }
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("FLOOD"))
                {
                    LoggerService.Warning($"FLOOD ERROR. Wait for: {ex.Message.Split("FLOOD_WAIT_")[1].Split("_")[0]}");
                }

                LoggerService.Error($"Telegram.Service.displayMessage: [{ex.GetLine()}] [{ex.Source}]\n\t{ex.Message}");
            }
        }

        private string? User(long id) => _users.TryGetValue(id, out var user) ? user.ToString() : $"User {id}";
        private string? Chat(long id) => _chats.TryGetValue(id, out var chat) ? chat.ToString() : $"Chat {id}";
        private string? Peer(Peer peer) => peer is null ? null : peer is PeerUser user ? User(user.user_id)
            : peer is PeerChat or PeerChannel ? Chat(peer.ID) : $"Peer {peer.ID}";
    }
}
