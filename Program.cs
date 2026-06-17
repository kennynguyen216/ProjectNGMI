using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;


var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10)
};

IChatClient ollamaClient = new OllamaApiClient(httpClient, "qwen3:8b");

// define agents here

AIAgent timeAgent = ollamaClient.AsAIAgent(
    instructions: "You analyze time and space complexity of the code snippet. Nothing else",
    tools: [AIFunctionFactory.Create(Tools.RunCode)]
);

AIAgent edgeAgent = ollamaClient.AsAIAgent(
    instructions: "You analyze edge cases and where the code snippet may fail or glitch. Nothing else",
    tools: [AIFunctionFactory.Create(Tools.RunCode)]
);


// conversation loop here

while (true)
{
    Console.WriteLine("Enter Solution");
    string? userResponse = Console.ReadLine();
    if(userResponse == null) break;

    Console.WriteLine(await timeAgent.RunAsync(userResponse));
    Console.WriteLine(await edgeAgent.RunAsync(userResponse));


}
