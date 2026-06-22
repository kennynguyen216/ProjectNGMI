# NGMI — Nonstop Grader of Messy Implementations

Built by Kennedy Nguyen as part of onboarding at LSU CARTS to learn the Microsoft Agent Framework (MAF).

NGMI is a multi-agent code evaluation system that takes a C# code snippet, compiles it, analyzes its time/space complexity, and identifies edge cases where it might fail.

## How It Works

Submissions are processed through a sequential workflow pipeline:

```
flowchart TD
  RunCodeExecutor["RunCodeExecutor (Start)"];
  TimeAgentExecutor["TimeAgentExecutor"];
  EdgeAgentExecutor["EdgeAgentExecutor"];
  Error["This does not run ❌"];
  Response["HTTP 200 Response ✅"];
  RunCodeExecutor -->|Code compiled| TimeAgentExecutor;
  RunCodeExecutor -->|Compilation error| Error;
  TimeAgentExecutor -->|Success| EdgeAgentExecutor;
  TimeAgentExecutor -->|Compile error propagated| Error;
  EdgeAgentExecutor --> Response;
```

- **RunCodeExecutor** — compiles and runs the snippet using Roslyn. Fails fast if it doesn't compile.
- **TimeAgentExecutor** — sends the code to an LLM agent for time/space complexity analysis.
- **EdgeAgentExecutor** — sends the code + complexity analysis to a second LLM agent to identify edge cases.

## Stack

- .NET 10 / C#
- Microsoft Agent Framework (MAF)
- Ollama (qwen3:8b running locally)
- Roslyn C# Scripting
- SQLite (analysis history + duplicate detection)
- ASP.NET Core Minimal API

## Running It

Start the server:

```
dotnet run
```

Submit a code snippet:

```powershell
Invoke-WebRequest -Method POST -Uri http://localhost:5000/analyze -ContentType "application/json" -Body '{"Code": "public static int Add(int a, int b) { return a + b; }"}'
```

Duplicate submissions are detected and skipped automatically.

## MAF Concepts Covered

- AIAgent with tools (AIFunctionFactory)
- Agent run middleware (logging, guardrails, result override, exception handling)
- IChatClient middleware
- Streaming with RunStreamingAsync
- Sessions and multi-turn conversations
- SQLite persistence
- Workflows with WorkflowBuilder and InProcessExecution
- Conditional branching in workflow executors
- ASP.NET Core hosting
