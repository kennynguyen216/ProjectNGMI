using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;


var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10)
};

const string ModelName = "qwen3:8b";

IChatClient baseClient = new OllamaApiClient(httpClient, ModelName);

IChatClient ollamaClient = baseClient
    .AsBuilder()
    .Use(getResponseFunc: Middleware.CustomChatClientMiddleware, getStreamingResponseFunc: null)
    .Build();

// memory time

string pastMemory =  File.ReadAllText("memory.txt");


// define agents here

AIAgent timeAgent = ollamaClient.AsAIAgent(
    instructions: $"You analyze time and space complexity of the code snippet. If the user gives code, call RunCode before answering. If the user asks about weather, call GetWeather before answering. Do not answer from memory when a tool is available. Past analyses:\n{pastMemory}",
    tools: [AIFunctionFactory.Create(Tools.RunCode), AIFunctionFactory.Create(Tools.GetWeather)]
).AsBuilder()
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Build();

AIAgent edgeAgent = ollamaClient.AsAIAgent(
    instructions: $"You analyze edge cases and where the code snippet may fail or glitch. If the user gives code, call RunCode before answering. Do not answer from memory when a tool is available. Past analyses:\n{pastMemory}",
    tools: [AIFunctionFactory.Create(Tools.RunCode)]
).AsBuilder()
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Build();


// conversation loop here

while (true)
{
    Console.WriteLine("Enter Solution");
  
    string? userResponse = Console.ReadLine();
    if(userResponse == null) break;

   

// checking if we already did this 
    if(pastMemory.Contains(userResponse)) {
        
        Console.WriteLine("Already analyzed this one. Nice tokens.");
        continue;

    };

    string executionResult = await Tools.RunCode(userResponse);
    string fullPrompt = $"{userResponse}\n\nExecution result: {executionResult}";
    AgentSession session = await timeAgent.CreateSessionAsync();

    await foreach(var update in timeAgent.RunStreamingAsync(fullPrompt, session))
    {
        Console.Write(update);
    }
    Console.WriteLine();
    Console.WriteLine(await edgeAgent.RunAsync(fullPrompt));
    File.AppendAllText("memory.txt", $"- Analyzed: {userResponse}\n");

    while (true)
    {
        Console.WriteLine("Follow up or type 'done':");
        string? followUp = Console.ReadLine();
        if (followUp == null || followUp == "done") break;
        await foreach (var update in timeAgent.RunStreamingAsync(followUp, session))
        {
            Console.Write(update);
        }
        Console.WriteLine();
    }

}
