using System.Net;
using Newtonsoft.Json;
using NumericWordsConversion;
using TTSButSpecificAudioDevice;

Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"))!;
Dictionary<string, string> replacedWords = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("dict.json"))!;

HttpListener listener = new()
{
    Prefixes = { $"http://{config.HttpAddress}:{config.HttpPort}/" }
};
    
try
{
    listener.Start();
}
catch (System.Net.Sockets.SocketException)
{
    Console.WriteLine($"Unable to start HTTP server on {config.HttpAddress}:{config.HttpPort}. More than likely, this port is already being used on this address.");
    throw;
}

AudioPlaybackEngine dummy = new();

await Task.Run(async () =>
{
    while (true)
    {
        try
        {
            HttpListenerContext context = await listener.GetContextAsync();
            await HandleContext(context);

            context.Response.StatusCode = 200;
            context.Response.KeepAlive = false;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    // ReSharper disable once FunctionNeverReturns
});
return;

static IEnumerable<string> SplitAlpha(string input)
{
    List<string> words = [string.Empty];
    for (int i = 0; i < input.Length; i++)
    {
        words[^1] += input[i];
        if (i + 1 < input.Length && char.IsLetter(input[i]) != char.IsLetter(input[i + 1]) && char.IsPunctuation(input[i]) == char.IsPunctuation(input[i + 1]))
        {
            words.Add(string.Empty);
        }
    }
    return words;
}

async Task HandleContext(HttpListenerContext context)
{
    if (context.Request.HttpMethod != "POST")
    {
        return;
    }

    using StreamReader reader = new(context.Request.InputStream, context.Request.ContentEncoding);
    SpeechMessage speechMessage = JsonConvert.DeserializeObject<SpeechMessage>(await reader.ReadToEndAsync());
    
    string[] parts = speechMessage.Text.Split(" ");
    List<string> output = [];
    foreach (string part in parts)
    {
        if (replacedWords.TryGetValue(part, out string? replaced))
        {
            string[] replacedParts = replaced.Split(" ");
            foreach (string replacedPart in replacedParts)
            {
                output.AddRange(SplitAlpha(replacedPart));
            }
        }
        else
        {
            output.AddRange(SplitAlpha(part));   
        }
    }

    for (int idx = 0; idx < output.Count; idx++)
    {
        char lastChar = output[idx].Last();
        
        string wordNoPunctuation = string.Join("", output[idx].Where(c => !c.Equals('?') && !c.Equals('!') && !c.Equals('-')));
        if (!decimal.TryParse(wordNoPunctuation, out decimal value))
        {
            continue;
        }
        
        NumericWordsConverter numericWordsConverter = new(new NumericWordsConversionOptions
        {
            Culture = Culture.International,
            DecimalSeparator = "point",
            DecimalPlaces = value.Scale
        });
        
        output[idx] = numericWordsConverter.ToWords(value);
        if (char.IsPunctuation(lastChar))
        {
            output[idx] = $"{output[idx]}{lastChar}";
        }
    }
    speechMessage.Text = string.Join(" ", output);
    Console.WriteLine($"[OUTPUT] {speechMessage.Text}");
    
    AudioPlaybackEngine.AddMessageToQueue(speechMessage);
}

[JsonObject(MemberSerialization.OptIn)]
internal struct SpeechMessage
{
    [JsonProperty("voice")] public string Voice { get; set; }
    [JsonProperty("text")] public string Text { get; set; }
    [JsonProperty("rate")] public int? Rate { get; set; }
}