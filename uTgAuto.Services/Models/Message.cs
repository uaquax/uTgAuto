namespace uTgAuto.Services.Models
{
    public class Message
    {
        public string? Text { get; set; }
        public List<string> Targets { get; set; } = new List<string>() { "@string" };
        public string? Answer { get; set; }
        public string? Target { get; set; }
        public bool IsWait { get; set; } = false;
        public bool AskAI { get; set; } = false;
        public TimeSpan SleepTime { get; set; } = new TimeSpan(0, 0, 1);
    }
}
