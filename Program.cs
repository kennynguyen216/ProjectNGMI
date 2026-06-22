using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;
using Microsoft.Agents.AI.Workflows;

var builder = WebApplication.CreateBuilder(args);

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

Database.Initialize();
string pastMemory = Database.GetPastAnalyses();

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

var runCode = new RunCodeExecutor();
var timeExec = new TimeAgentExecutor(timeAgent);
var edgeExec = new EdgeAgentExecutor(edgeAgent);

var app = builder.Build();

app.MapPost("/analyze", async (AnalyzeRequest request) =>
{
    string userCode = request.Code;

    if (Database.IsAlreadyAnalyzed(userCode))
        return Results.Ok("Already analyzed this one. Nice tokens.");

    var workflowBuilder = new WorkflowBuilder(runCode);
    workflowBuilder.AddEdge(runCode, timeExec).AddEdge(timeExec, edgeExec).WithOutputFrom(edgeExec);
    var workflow = workflowBuilder.Build();

    await using var run = await InProcessExecution.RunAsync(workflow, userCode);
    string result = "";
    foreach (var evt in run.NewEvents)
    {
        if (evt is ExecutorCompletedEvent completed)
            result = completed.Data?.ToString() ?? "";
    }

    Database.SaveAnalysis(userCode, result);
    return Results.Ok(result);
});

app.Run();

record AnalyzeRequest(string Code);

