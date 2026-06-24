using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;
using Microsoft.Agents.AI.Workflows;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var options = new OpenAIClientOptions()
{
    Endpoint = new Uri(builder.Configuration["Endpoint"])
};

string ModelName = builder.Configuration["ModelName"];

var chatClient = new OpenAIClient(new ApiKeyCredential("tiger123"), options)
.GetChatClient(ModelName)
.AsIChatClient()
.AsBuilder()
.Use(getResponseFunc: Middleware.CustomChatClientMiddleware, getStreamingResponseFunc: null)
.Build();


Database.Initialize();

static Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchPastAnalyses(string query, CancellationToken cancellationToken)
{
    var keywords = query.Split([' ', '(', ')', '{', '}', ';'], StringSplitOptions.RemoveEmptyEntries);
    var results = Database.SearchByKeywords(keywords);
    return Task.FromResult(results.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(r => new TextSearchProvider.TextSearchResult { Text = r }));
}


var ragOptions = new TextSearchProviderOptions
{
    SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke
};

AIAgent timeAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new() { Instructions = "You analyze time and space complexity of code snippets. The code and its execution result are already provided in the prompt. Do not call any tools. Just analyze and respond." },
    AIContextProviders = [new TextSearchProvider(SearchPastAnalyses, ragOptions)]
}).AsBuilder()
    .Use(runFunc: Middleware.GuardrailMiddleware, runStreamingFunc: null)
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Use(runFunc: Middleware.ResultOverrideMiddleware, runStreamingFunc: null)
    .Build();

AIAgent edgeAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new() { Instructions = "You analyze edge cases where the code may fail or glitch. The code and its execution result are already provided in the prompt. Do not call any tools. Just analyze and respond." },
    AIContextProviders = [new TextSearchProvider(SearchPastAnalyses, ragOptions)]
}).AsBuilder()
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

    // if (Database.IsAlreadyAnalyzed(userCode))
    //     return Results.Ok("Already analyzed this one. Nice tokens.");

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
