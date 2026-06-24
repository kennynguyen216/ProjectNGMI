# NGMI Progress

## Completed Features

1. Two specialized agents (timeAgent for complexity, edgeAgent for edge cases) using Microsoft Agent Framework
2. Tools with AIFunctionFactory.Create — RunCode (Roslyn) and GetWeather
3. Roslyn C# Scripting for real-time code compilation and execution with proper namespace imports
4. Function calling middleware — logs tool name and result
5. Agent run middleware (non-streaming) — logs every agent run start and end
6. Agent run streaming middleware — intercepts streaming runs
7. IChatClient middleware — intercepts raw calls to Ollama, logs message count
8. Guardrail middleware — blocks non-code inputs before the model runs
9. Streaming responses with RunStreamingAsync on timeAgent
10. Sessions and multi-turn follow-up conversation loop
11. SQLite database replacing memory.txt — stores code + analysis + timestamp
12. Duplicate detection via database query instead of string match

13. Result override middleware — appends NGMI footer to all agent responses
14. Exception handling middleware on edgeAgent — catches timeouts gracefully
15. Workflows — RunCodeExecutor → TimeAgentExecutor → EdgeAgentExecutor pipeline using WorkflowBuilder and InProcessExecution

## Up Next

- Test and verify workflow runs end-to-end

user input → RunCodeExecutor → TimeAgentExecutor → EdgeAgentExecutor → output


- Embeddings for semantic deduplication (cosine similarity over code vectors)
- ASP.NET Core API conversion
- Remaining middleware: Shared State, Runtime Context, Agent vs Run Scope
