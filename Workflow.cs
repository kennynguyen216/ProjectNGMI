using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

class RunCodeExecutor() : Executor<string, string>("RunCodeExecutor")
{
    public override async ValueTask<string> HandleAsync(string code, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string executionResult = await Tools.RunCode(code);
        return $"{code}\n\nExecution result: {executionResult}";
    }
}


class TimeAgentExecutor(AIAgent agent) : Executor<string, string> ("TimeAgentExecutor")
{
    public override async ValueTask<string> HandleAsync(string fullPrompt, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var response = await agent.RunAsync(fullPrompt, cancellationToken : cancellationToken);
        return fullPrompt + "\n\n" + response.Text ?? "";

    }
    
}

class EdgeAgentExecutor(AIAgent agent) : Executor<string, string> ("EdgeAgentExecutor")
{
    public override async ValueTask<string> HandleAsync(string fullPrompt, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var response = await agent.RunAsync(fullPrompt, cancellationToken : cancellationToken);
        return response.Text ?? "";
    }


}