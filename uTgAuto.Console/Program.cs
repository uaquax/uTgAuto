using uTgAuto.Services;

int workerThreads, completionPortThreads;
ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);

Console.WriteLine("Max worker threads: {0}", workerThreads);
Console.WriteLine("Max completion port threads: {0}", completionPortThreads);

var service = new BotService();