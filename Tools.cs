using System.ComponentModel;
class Tools
{
    public static string RunCode(string code)
    {
        Console.WriteLine("[TOOL CALLED] RunCode invoked");
        return $"Stub: executed code snippet. No runtime errors detected.";
    }

    [Description("Get the weather for the given location")]
    public static string GetWeather([Description("The location to get the weather for.")] string location)
    {
        Console.WriteLine($"The weather in {location} is pretty nice");
        return $"Stub: executed code snippet. No runtime errors detected.";
    }




}