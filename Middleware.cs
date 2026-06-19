using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Linq;
using System.Runtime.CompilerServices;


class Middleware
{
    public static async Task<AgentResponse> CustomAgentRunMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[RUN MIDDLEWARE] Agent Starting. Input: {string.Join(" ", messages.Select(message => message.Text))}");
        var result = await innerAgent.RunAsync(messages, session, options, cancellationToken);
        Console.WriteLine($"[Run MIDDLEWARE] Agent Done. Out: {result.Text}");
        return result;
        
    }

    public static async IAsyncEnumerable<AgentResponseUpdate> CustomAgentRunStreamingMiddleware (
        IEnumerable<ChatMessage> messages,
        AgentSession? session, 
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        Console.WriteLine("[RUN Middleware] Streaming started.");
        await foreach(var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            yield return update;
        }
        Console.WriteLine("[RUN Middleware] Streaming done.");
    }
    public static async ValueTask<object?> LoggingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, 
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[MIDDLEWARE] Tool called: {context.Function.Name}");
        var result = await next(context, cancellationToken);
        Console.WriteLine($"[MIDDLEWARE] Tool result: {result}");
        return result;

    }






}