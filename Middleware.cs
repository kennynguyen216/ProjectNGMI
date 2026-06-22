using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Linq;
using System.Runtime.CompilerServices;


class Middleware
{
// Fires on every non-streaming agent run, logs the unput and output 
    public static async Task<AgentResponse> CustomAgentRunMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[RUN MIDDLEWARE] Agent Starting. Input: {string.Join(" ", messages.Select(message => message.Text))}");
        var result = await innerAgent.RunAsync(messages, session, options, cancellationToken);
        Console.WriteLine("[Run MIDDLEWARE] Agent Done.");
        return result;
        
    }

    public static async Task<AgentResponse> ResultOverrideMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options, 
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
        var modifiedMessages = response.Messages.Select(msg =>
        msg.Role == ChatRole.Assistant && msg.Text is not null
        ? new ChatMessage(ChatRole.Assistant, msg.Text + "\n\n--- Analysis by NGMI ---")
        : msg
        ).ToList();
        return new AgentResponse(modifiedMessages);

        
    }

// guardrail function to see if the code snippet is actually code
    public static async Task<AgentResponse> GuardrailMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options, 
        AIAgent innerAgent, 
        CancellationToken cancellationToken)
    {
      string input = string.Join(" ", messages.Select(m => m.Text));
    if (!input.Contains('{') || !input.Contains('}'))
    {
        Console.WriteLine("[GUARDRAIL] Blocked: input is not a code snippet.");
        return new AgentResponse([new ChatMessage(ChatRole.Assistant, "Please submit a C# code snippet.")]);
    }
    return await innerAgent.RunAsync(messages, session, options, cancellationToken);
    }
//yields chunks as they arrive 
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
// logs how many messages are being sent to the model 
    public static async Task<ChatResponse> CustomChatClientMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient, 
        CancellationToken cancellationToken
    )
    {
        Console.WriteLine($"[Chat Middleware] :{messages.Count()} messages to Ollama. ");
        var result = await innerChatClient.GetResponseAsync(messages, options, cancellationToken);
        Console.WriteLine("[Chat Middleware] Response Received.");
        return result;
    
    }
//Fires whenever the function calls a tool 
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