using System.Net;
using System.Runtime.Versioning;
using Newtonsoft.Json;
using TTSButSpecificAudioDevice;

Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"))!;

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

static async Task HandleContext(HttpListenerContext context)
{
    if (context.Request.HttpMethod != "POST")
    {
        return;
    }

    using StreamReader reader = new(context.Request.InputStream, context.Request.ContentEncoding);
    SpeechMessage speechMessage = JsonConvert.DeserializeObject<SpeechMessage>(await reader.ReadToEndAsync());
    
    AudioPlaybackEngine.AddMessageToQueue(speechMessage);
}

[JsonObject(MemberSerialization.OptIn)]
internal struct SpeechMessage
{
    [JsonProperty("voice")] public string Voice { get; set; }
    [JsonProperty("text")] public string Text { get; set; }
    [JsonProperty("rate")] public int? Rate { get; set; }
}