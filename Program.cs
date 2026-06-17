using Microsoft.Extensions.AI;
using OllamaSharp;
using Microsoft.Agents.AI;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10)
};

IChatClient ollamaClient = new OllamaApiClient(httpClient, "qwen3:8b");
//warns me im using ollama and its lowk foreign but im saying dont worry
#pragma warning disable SKEXP0070
var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IChatCompletionService>(ollamaClient.AsChatCompletionService());
var kernel = builder.Build();
#pragma warning restore SKEXP0070


var bigOAgent = new ChatCompletionAgent
{
    Name = "BigOOptimizer",
    Instructions = "Only analyze space and time complexity. Nothing else",
    Kernel = kernel
};

var edgeAgent = new ChatCompletionAgent
{
    Name = "EdgeCaseDestroyer",
    Instructions = "Only analyze edge cases of code snippet. Nothing else",
    Kernel = kernel
};

var bigOThread = new ChatHistoryAgentThread();

var edgeThread = new ChatHistoryAgentThread();


while(true)
{
    
    Console.Write("Enter solution");
    string? userResponse = Console.ReadLine();
    if(userResponse == null) break;

    await foreach (var message in bigOAgent.InvokeAsync(userResponse,bigOThread))
    {
        Console.WriteLine(message.Message.Content);
    }

    await foreach (var edgeMessage in edgeAgent.InvokeAsync(userResponse, edgeThread))
    {
        Console.WriteLine(edgeMessage.Message.Content);
    }

}
