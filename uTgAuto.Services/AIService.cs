using ChatGPT.Net;
using dotenv.net;

namespace uTgAuto.Services
{
    public class AIService
    {
        private readonly string _information;
        private readonly ChatGpt _bot;

        public AIService(string information)
        {
            _information = information;
            DotEnv.Load();
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            _bot = new ChatGpt(apiKey);
        }

        public async Task<string> Ask(string text)
        {
            return await _bot.Ask($"Hi, answer a question from first person view, by the information I'll give to you. But you need to remember that you need to leave ONLY the answer, you don't need any extra words such as: by the information you gave to me, etc. Here is the information: {_information}. And here is the question: {text}", "default");
        }
    }
}
