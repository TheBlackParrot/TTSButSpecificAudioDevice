using Newtonsoft.Json;

namespace TTSButSpecificAudioDevice;

[JsonObject(MemberSerialization.OptIn)]
internal class Config
{
    [JsonProperty] internal string AudioDeviceGuid { get; set; } = null!;
    [JsonProperty] internal string HttpAddress { get; set; } = null!;
    [JsonProperty] internal int HttpPort { get; set; }
    [JsonProperty] internal decimal StartEstimatingNumbersAt { get; set; } = 1000000000000;
} 