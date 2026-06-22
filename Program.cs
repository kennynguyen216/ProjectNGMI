using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;
using Microsoft.Agents.AI.Workflows;

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

Database.Initialize();
string pastMemory = Database.GetPastAnalyses();


// define agents here

AIAgent timeAgent = ollamaClient.AsAIAgent(
    instructions: $"You analyze time and space complexity of the code snippet. If the user gives code, call RunCode before answering. If the user asks about weather, call GetWeather before answering. Do not answer from memory when a tool is available. Past analyses:\n{pastMemory}",
    tools: [AIFunctionFactory.Create(Tools.RunCode), AIFunctionFactory.Create(Tools.GetWeather)]
).AsBuilder()
    .Use(runFunc: Middleware.GuardrailMiddleware, runStreamingFunc: null)
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Use(runFunc: Middleware.ResultOverrideMiddleware, runStreamingFunc: null)
    .Build();

AIAgent edgeAgent = ollamaClient.AsAIAgent(
    instructions: $"You analyze edge cases and where the code snippet may fail or glitch. If the user gives code, call RunCode before answering. Do not answer from memory when a tool is available. Past analyses:\n{pastMemory}",
    tools: [AIFunctionFactory.Create(Tools.RunCode)]
).AsBuilder()
    .Use(runFunc: Middleware.ExceptionHandlingMiddleware, runStreamingFunc: null)
    .Use(runFunc: Middleware.GuardrailMiddleware, runStreamingFunc: null)
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Use(runFunc: Middleware.ResultOverrideMiddleware, runStreamingFunc: null)
    .Build();


// define workflow executors
var runCode = new RunCodeExecutor();
var timeExec = new TimeAgentExecutor(timeAgent);
var edgeExec = new EdgeAgentExecutor(edgeAgent);

// conversation loop here

while (true)
{
    Console.WriteLine("Enter Solution");
  
    string? userResponse = Console.ReadLine();
    if(userResponse == null) break;

   

// checking if we already did this 
    if(Database.IsAlreadyAnalyzed(userResponse)) {
        
        Console.WriteLine("Already analyzed this one. Nice tokens.");
        continue;

    };

    var workflowBuilder = new WorkflowBuilder(runCode);
    workflowBuilder.AddEdge(runCode, timeExec).AddEdge(timeExec, edgeExec).WithOutputFrom(edgeExec);
    var workflow = workflowBuilder.Build();

    AgentSession session = await timeAgent.CreateSessionAsync();

    await using var run = await InProcessExecution.RunAsync(workflow, userResponse);
    foreach (var evt in run.NewEvents)
    {
        if(evt is ExecutorCompletedEvent completed)
            Console.WriteLine($"[{completed.ExecutorId}]: {completed.Data}");
    }
    Database.SaveAnalysis(userResponse, "workflow analysis");


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
