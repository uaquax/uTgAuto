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
        public float Coins { get; set; } = 0;
        public UserState State { get; set; } = UserState.Start;
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<ParallelMessage> ParallelMessages { get; set; } = new List<ParallelMessage>();
        public TelegramService? TelegramService { get; set; }
        public bool IsSignedUp { get; set; } = false;
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
        MessageAskAI,
        MessageInformation,
        MessageTargets,
        MessageAnswer,
        MessageSleepTime,
        MessagesConfirm,
        ParallelMessageTarget,
        ParallelMessageAskAI,
        ParallelMessageInformation,
        ParallelMessageTargets,
        ParallelMessageAnswer,
        ParallelMessageConfirm,
        SignInTelegramService,
        Ready,
        Running
    }
}
