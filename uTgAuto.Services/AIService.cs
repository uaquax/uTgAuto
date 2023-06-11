using ChatGPT.Net;
using dotenv.net;

namespace uTgAuto.Services
{
    public static class AIService
    {
        private static readonly ChatGpt _bot;

        static AIService()
        {
            DotEnv.Load();
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            _bot = new ChatGpt(apiKey);
        }

        public static async Task<string> Ask(string information, string text)
        {
            return await _bot.Ask($"Hi, answer a question from first person view, by the information I'll give to you. But you need to remember that you need to leave ONLY the answer, you don't need any extra words such as: by the information you gave to me, etc. Here is the information: {information}. And here is the question: {text}", "default");
        }
    }
}
