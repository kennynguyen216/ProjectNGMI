using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

class Tools
{
    public static async Task<string> RunCode(string code)
    {
        

        try
        {
            string codeWrapper = $"class Submission {{{code}}}";
            await CSharpScript.RunAsync(codeWrapper);
            return "Code executed. Good job";
        }
        catch (CompilationErrorException ex)
        {
            return $"Compilation error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Runtime exception {ex.Message}";
        }
    }

    [Description("Get the weather for the given location")]
    public static string GetWeather([Description("The location to get the weather for.")] string location)
    {
        Console.WriteLine($"The weather in {location} is pretty nice");
        return $"The weather in {location} is sunny and 72°F.";

    }




}