namespace uTgAuto.Services.Models
{
    public class User
    {
        public int ID { get; set; } = 0;
        public long ChatID { get; set; } = 0;
        public int ApiID { get; set; } = 0;
        public string ApiHash { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int Credits { get; set; } = 1;
        public UserState State { get; set; } = UserState.Start;
        public List<Message> Messages { get; set; } = new List<Message>();
        public TelegramService? TelegramService { get; set; }
    }

    public enum UserState
    {
        Start,
        SignUpVerify,
        SignUpPhone,
        SignUpApiID,
        SignUpApiHash,
        SignUpPassword,
        MessageText,
        MessageTarget,
        MessageIsWait,
        MessageTargets,
        MessageAnswer,
        MessageAskAI,
        MessageSleepTime,
        MessagesConfirm,
        SignInTelegramService,
        Ready,
        Running
    }
}
