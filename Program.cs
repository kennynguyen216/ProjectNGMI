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
    ChatOptions = new() { Instructions = "You analyze edge cases where the code may fail or glitch. The code and its execution result are already provided in the prompt. Do not call any tools. Just analyze and respond. If you find any edge cases or issues, end your response with ISSUES_FOUND. If the code handles all edge cases correctly, end with NO_ISSUES." },
    AIContextProviders = [new TextSearchProvider(SearchPastAnalyses, ragOptions)]
}).AsBuilder()
    .Use(runFunc: Middleware.ExceptionHandlingMiddleware, runStreamingFunc: null)
    .Use(runFunc: Middleware.GuardrailMiddleware, runStreamingFunc: null)
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Use(runFunc: Middleware.ResultOverrideMiddleware, runStreamingFunc: null)
    .Build();

AIAgent hintAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new() { Instructions = "You give hints to help the user fix edge cases in their code without giving away the full solution. Be concise and Socratic." },
}).AsBuilder()
    .Use(Middleware.LoggingMiddleware)
    .Use(runFunc: Middleware.CustomAgentRunMiddleware, runStreamingFunc: Middleware.CustomAgentRunStreamingMiddleware)
    .Build();

var app = builder.Build();

Workflow CreateWorkflow(TaskCompletionSource<bool> approval, TaskCompletionSource<string> paused)
{
    var runCode = new RunCodeExecutor();
    var timeExec = new TimeAgentExecutor(timeAgent);
    var humanExec = new HumanReviewExecutor(approval, paused);
    var edgeExec = new EdgeAgentExecutor(edgeAgent);
    var hintExec = new HintAgentExecutor(hintAgent);
    var compilationErrorExec = new CompilationErrorExecutor();

    return new WorkflowBuilder(runCode)
        .AddEdge<CompilationResult>(runCode, timeExec, condition: result => result is { Success: true })
        .AddEdge(timeExec, humanExec)
        .AddEdge(humanExec, edgeExec)
        .AddEdge<CompilationResult>(runCode, compilationErrorExec, condition: result => result is { Success: false })
        .AddEdge<EdgeResult>(edgeExec, hintExec, condition: result => result.HasIssues)
        .WithOutputFrom(hintExec, edgeExec, compilationErrorExec)
        .Build();
}

CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();
var sessions = new Dictionary<string, (TaskCompletionSource<bool> Approval, Task<string> Completion, string Code)>();




app.MapPost("/analyze", async (AnalyzeRequest request) =>
{
    string userCode = request.Code;

    // if (Database.IsAlreadyAnalyzed(userCode))
    //     return Results.Ok("Already analyzed this one. Nice tokens.");

    var approval = new TaskCompletionSource<bool>();
    var paused = new TaskCompletionSource<string>();
    var workflow = CreateWorkflow(approval, paused);


    Console.WriteLine(workflow.ToMermaidString());

    var run = await InProcessExecution.RunStreamingAsync(workflow, userCode, checkpointManager);

    var completionTask = Task.Run(async () =>
    {
        string result = "";
        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent output)
                result = output.Data?.ToString() ?? "";
        }
        return result;
    });

    string partialResult = await paused.Task;
    string sessionId = Guid.NewGuid().ToString();
    sessions[sessionId] = (approval, completionTask, userCode);

    return Results.Ok(new { sessionId, partialResult });
});

app.MapPost("/resume", async (ResumeRequest request) =>
{
    if (!sessions.TryGetValue(request.SessionId, out var session))
        return Results.NotFound("Session not found.");

    sessions.Remove(request.SessionId);
    session.Approval.SetResult(request.Approved);

    if (!request.Approved)
        return Results.Ok("Analysis cancelled.");

    string finalResult = await session.Completion;
    Database.SaveAnalysis(session.Code, finalResult);
    return Results.Ok(finalResult);
});

app.Run();

record AnalyzeRequest(string Code);
record ResumeRequest(string SessionId, bool Approved);
