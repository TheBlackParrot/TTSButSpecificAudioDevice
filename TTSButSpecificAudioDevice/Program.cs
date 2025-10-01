﻿using System.Globalization;
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

// doing my best using wikipedia here lol
Dictionary<char, string[]> currencyWords = new()
{
    {'$', ["dollar", "cent"]},
    {'\u20ac', ["euro", "cent"]},
    {'\u00a3', ["pound", "pence"]},
    {'\u20a4', ["pound", "pence"]},
    {'\u20bd', ["ruble", "kopeck"]},
    {'\u20b9', ["rupee", "paise"]},
    {'\u00a5', ["yen"]},
    {'\u00a4', ["scarab"]}
};

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
        switch (input[i])
        {
            case '%':
                words.Add("percent");
                break;
            
            case ':':
                words.Add(string.Empty);
                continue;
            
            default:
                words[^1] += input[i];
                break;
        }

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
    
    string json = await reader.ReadToEndAsync();
    json = json.Replace(@"\", @"\\");
    
    SpeechMessage speechMessage = JsonConvert.DeserializeObject<SpeechMessage>(json);
    
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
        char lastChar;
        try
        {
            lastChar = output[idx].Last();
        }
        catch (InvalidOperationException)
        {
            continue;
        }

        string wordNoPunctuation = string.Join("", output[idx].Where(c => !c.Equals('?') && !c.Equals('!') && !char.IsSymbol(c)));
        if (!decimal.TryParse(wordNoPunctuation, out decimal value))
        {
            continue;
        }

        dynamic numericWordsConverter;
        if (char.IsSymbol(output[idx].First()))
        {
            if (currencyWords.TryGetValue(output[idx].First(), out string[]? currency))
            {
                bool plural = (int)value != 1;
                bool subIsPlural = ((int)(value * 100) % 100) != 1;
                numericWordsConverter = new CurrencyWordsConverter(new CurrencyWordsConversionOptions
                {
                    Culture = Culture.International,
                    OutputFormat = OutputFormat.English,
                    DecimalSeparator = "point",
                    DecimalPlaces = value.Scale,
                    CurrencyUnit = plural ? $"{currency[0]}s" : currency[0],
                    EndOfWordsMarker = "",
                    SubCurrencyUnit = currency.Length > 1 ? (subIsPlural ? $"{currency[1]}s" : currency[1]) : ""
                });
                
                goto finishConverting;
            }
        }
        
        numericWordsConverter = new NumericWordsConverter(new NumericWordsConversionOptions
        {
            Culture = Culture.International,
            DecimalSeparator = "point",
            DecimalPlaces = value.Scale
        });
        
        finishConverting:
            bool isNegative = value < 0;
            output[idx] = numericWordsConverter.ToWords(Math.Abs(value));
            if (isNegative)
            {
                output[idx] = $"negative {output[idx]}";
            }
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