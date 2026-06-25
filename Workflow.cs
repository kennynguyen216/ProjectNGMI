using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

record CompilationResult(string Code, bool Success, string Output);
record EdgeResult(string Analysis, bool HasIssues);

class RunCodeExecutor() : Executor<string, CompilationResult>("RunCodeExecutor")
{
    public override async ValueTask<CompilationResult> HandleAsync(
        string code,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string executionResult = await Tools.RunCode(code);
        await context.QueueStateUpdateAsync("originalCode", code, scopeName: "SharedCode", cancellationToken);
        bool success = executionResult.Contains("Code executed. Good job");

        return new CompilationResult(code, success, executionResult);
    }
}

class CompilationErrorExecutor() : Executor<CompilationResult, string>("CompilationErrorExecutor")
{
    public override ValueTask<string> HandleAsync(
        CompilationResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult($"The submitted code could not be analyzed.\n\n{result.Output}");
    }
}

class TimeAgentExecutor(AIAgent agent) : Executor<CompilationResult, string>("TimeAgentExecutor")
{
    public override async ValueTask<string> HandleAsync(
        CompilationResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string fullPrompt = $"{result.Code}\n\nExecution result: {result.Output}";
        var response = await agent.RunAsync(fullPrompt, cancellationToken: cancellationToken);
        return fullPrompt + "\n\n" + (response.Text ?? "");
    }
}

class EdgeAgentExecutor(AIAgent agent) : Executor<string, EdgeResult>("EdgeAgentExecutor")
{
    public override async ValueTask<EdgeResult> HandleAsync(
        string fullPrompt,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var originalCode = await context.ReadStateAsync<string>("originalCode", scopeName: "SharedCode", cancellationToken);
        Console.WriteLine($"[SHARED STATE] Original code: {originalCode}");
        var response = await agent.RunAsync(fullPrompt, cancellationToken: cancellationToken);
        bool hasIssues = (response.Text ?? "").Contains("ISSUES_FOUND");
        return new EdgeResult(response.Text ?? "", hasIssues);
    }
}

class HumanReviewExecutor(TaskCompletionSource<bool> approval, TaskCompletionSource<string> paused) : Executor<string, string>("HumanReviewExecutor")
{
    public override async ValueTask<string> HandleAsync(
        string input, 
        IWorkflowContext context,
        CancellationToken cancellationToken = default
    )
    {
        paused.SetResult(input);
        bool approved = await approval.Task;
        if(!approved) throw new OperationCanceledException("Rejected.");
        return input;
    }


}

class HintAgentExecutor(AIAgent agent) : Executor<EdgeResult, string>("HintAgentExecutor")
{
    public override async ValueTask<string> HandleAsync(
        EdgeResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var response = await agent.RunAsync(result.Analysis, cancellationToken: cancellationToken);
        return response.Text ?? "";

    }


}