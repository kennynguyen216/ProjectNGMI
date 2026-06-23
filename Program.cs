using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;
using Microsoft.Agents.AI.Workflows;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

var app = builder.Build();

Workflow CreateWorkflow()
{
    var runCode = new RunCodeExecutor();
    var timeExec = new TimeAgentExecutor(timeAgent);
    var edgeExec = new EdgeAgentExecutor(edgeAgent);
    var compilationErrorExec = new CompilationErrorExecutor();

    return new WorkflowBuilder(runCode)
        .AddEdge<CompilationResult>(runCode, timeExec, condition: result => result is { Success: true })
        .AddEdge(timeExec, edgeExec)
        .AddEdge<CompilationResult>(runCode, compilationErrorExec, condition: result => result is { Success: false })
        .WithOutputFrom(edgeExec, compilationErrorExec)
        .Build();
}

CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();



app.MapPost("/analyze", async (AnalyzeRequest request) =>
{
    string userCode = request.Code;

    if (Database.IsAlreadyAnalyzed(userCode))
        return Results.Ok("Already analyzed this one. Nice tokens.");

    var workflow = CreateWorkflow();

    Console.WriteLine(workflow.ToMermaidString());

    await using var run = await InProcessExecution.RunStreamingAsync(workflow, userCode,checkpointManager);
    string result = "";
    await foreach (var evt in run.WatchStreamAsync())
    {
        if (evt is WorkflowOutputEvent output)
            result = output.Data?.ToString() ?? "";
    }
Console.WriteLine($"[CHECKPOINTS] {run.Checkpoints.Count} checkpoints created.");

    Database.SaveAnalysis(userCode, result);
    return Results.Ok(result);
});

app.Run();

record AnalyzeRequest(string Code);
