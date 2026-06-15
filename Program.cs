using Microsoft.Extensions.AI;
using OllamaSharp;

IChatClient chatClient = new OllamaApiClient(
    new Uri("http://localhost:11434"),
    "qwen3:8b");

var response = await chatClient.GetResponseAsync(
    "Explain Big O notation in two sentences.");

Console.WriteLine(response.Text);