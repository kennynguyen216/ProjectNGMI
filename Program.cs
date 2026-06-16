using Microsoft.Extensions.AI;
using OllamaSharp;
using System;

// error if the model takes longer than 100 seconds to respond it times out this line fixes it
//makes it so that you can take 5 minute to respond 

var httpClient = new HttpClient{ 
    
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(10)
    
    };


IChatClient chatClient = new OllamaApiClient(httpClient, "qwen3:8b");


var history = new List<ChatMessage>();

var edgeHistory =  new List<ChatMessage>();

history.Add(new ChatMessage(ChatRole.System,"You are BigOOptimizer. You only analyze space and time complexity. You do not discuss anything else"));

edgeHistory.Add(new ChatMessage(ChatRole.System, "You are EdgeCaseDestroyer. You only analyze the edge cases of the code given and tell where they fail. You do not discuss anything else."));


while(true)
{
    
    Console.Write("Enter solution");
    string? userResponse = Console.ReadLine();
    if(userResponse == null) break;

    history.Add(new ChatMessage(ChatRole.User, userResponse));
    edgeHistory.Add(new ChatMessage(ChatRole.User, userResponse));

    //history.Add(new ChatMessage(ChatRole.User,"Analyze this code:\npublic int[] TwoSum(int[] nums, int target) { }"));

    var response = await chatClient.GetResponseAsync(history);
    var edgeResponse = await chatClient.GetResponseAsync(edgeHistory);


    Console.WriteLine(response.Text);
    Console.WriteLine(edgeResponse.Text);

    edgeHistory.AddMessages(edgeResponse);
    history.AddMessages(response);

}
