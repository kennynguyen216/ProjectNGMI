using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;


var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10)
};

IChatClient ollamaClient = new OllamaApiClient(httpClient, "qwen3:8b");

// memory time

string pastMemory =  File.ReadAllText("memory.txt");


// define agents here

AIAgent timeAgent = ollamaClient.AsAIAgent(
    instructions: $"You analyze time and space complexity of the code snippet. Nothing else... Past analyses:\n{pastMemory}",
    tools: [AIFunctionFactory.Create(Tools.RunCode), AIFunctionFactory.Create(Tools.GetWeather)]
);

AIAgent edgeAgent = ollamaClient.AsAIAgent(
    instructions: $"You analyze edge cases and where the code snippet may fail or glitch. Nothing else... Past analyses:\n{pastMemory}",
    tools: [AIFunctionFactory.Create(Tools.RunCode)]
);


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

    //this runs the code hard coded
    string executionResult = await Tools.RunCode(userResponse);
    string fullPrompt = $"{userResponse}\n\nExecution result: {executionResult}";

    Console.WriteLine(await timeAgent.RunAsync(fullPrompt));
    Console.WriteLine(await edgeAgent.RunAsync(fullPrompt));
    File.AppendAllText("memory.txt", $"- Analyzed: {userResponse}\n");

}
