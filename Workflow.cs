using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

record CompilationResult(string Code, bool Success, string Output);

class RunCodeExecutor() : Executor<string, CompilationResult>("RunCodeExecutor")
{
    public override async ValueTask<CompilationResult> HandleAsync(
        string code,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string executionResult = await Tools.RunCode(code);
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

class EdgeAgentExecutor(AIAgent agent) : Executor<string, string>("EdgeAgentExecutor")
{
    public override async ValueTask<string> HandleAsync(
        string fullPrompt,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var response = await agent.RunAsync(fullPrompt, cancellationToken: cancellationToken);
        return response.Text ?? "";
    }
}
