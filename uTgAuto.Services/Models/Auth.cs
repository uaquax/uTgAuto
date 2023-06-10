namespace uTgAuto.Services.Models
{
    public class Auth
    {
        public long ChatId { get; set; }
        public int ApiId { get; set; } = 0;
        public string ApiHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;

        public Auth(int apiId, string apiHash, string phoneNumber, string? password = null, long chatId = 0)
        {
            ApiId = apiId;
            ApiHash = apiHash;
            PhoneNumber = phoneNumber;
            Password = password;
            ChatId = chatId;
        }

        public string? Config(string what)
        {
            Directory.CreateDirectory("Sessions");
            switch (what)
            {
                case "session_pathname": return $"Sessions/{ChatId}.session";
                case "api_id": return ApiId.ToString();
                case "api_hash": return ApiHash;
                case "phone_number": return PhoneNumber;
                case "verification_code": return BotService.databaseService.GetUserByChatID(ChatId).Code;
                case "password": return Password ?? "";
                default: return null;
            }
        }
    }
}
