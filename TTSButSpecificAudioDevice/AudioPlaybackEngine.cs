using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;

namespace TTSButSpecificAudioDevice;

// https://markheath.net/post/fire-and-forget-audio-playback-with
internal class AudioPlaybackEngine : IDisposable
{
    private static readonly DirectSoundOut OutputDevice;
    private static readonly MixingSampleProvider Mixer;
    private static readonly WaveFormat OutputFormat = new(22050, 1);
    private static readonly SpeechSynthesizer Synthesizer = new();

    private static readonly Dictionary<string, string> Voices = [];

    private static readonly List<ISampleProvider> SampleProviders = [];
    private static bool _isPlaying;
    
    private static void ListAudioDevices()
    {
        foreach (DirectSoundDeviceInfo dev in DirectSoundOut.Devices)
        {
            Console.WriteLine($"{dev.Guid} | {dev.Description}");
        }
    }
    
    private static void InitVoices()
    {
        foreach (InstalledVoice voice in Synthesizer.GetInstalledVoices())
        {
            string name = voice.VoiceInfo.Name.Split(" ")[1];
            if (Voices.ContainsKey(name))
            {
                continue;
            }
            
            Voices[name] = voice.VoiceInfo.Name;
            Console.WriteLine($"{name} ({voice.VoiceInfo.Culture})");
        }
    }

    static AudioPlaybackEngine()
    {
        InitVoices();
        
        Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"))!;
    
        bool tryParse = Guid.TryParse(config.AudioDeviceGuid, out Guid guid);
        if (!tryParse)
        {
            Console.WriteLine($"Audio device {config.AudioDeviceGuid} not found, please use one of the GUIDs below:");
            ListAudioDevices();
            throw new Exception("Invalid audio device");
        }
        
        OutputDevice = new DirectSoundOut(guid);
        Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(22050, 1))
        {
            ReadFully = true
        };

        Mixer.MixerInputEnded += (_, _) =>
        {
            Console.WriteLine("ended");
            _isPlaying = false;
            ProcessQueue();
        };
        
        OutputDevice.Init(Mixer);
        OutputDevice.Play();
    }

    private static void ProcessQueue()
    {
        if (SampleProviders.Count == 0 || _isPlaying)
        {
            return;
        }

        _isPlaying = true;
        ISampleProvider sample = SampleProviders[0];
        SampleProviders.RemoveAt(0);
        AddMixerInput(sample);
    }

    public static void AddMessageToQueue(SpeechMessage speechMessage)
    {
        using MemoryStream memoryStream = new();
        
        Synthesizer.SelectVoice(Voices[speechMessage.Voice]);
        Synthesizer.Rate = speechMessage.Rate ?? 0;
        Synthesizer.SetOutputToAudioStream(memoryStream, new SpeechAudioFormatInfo(22050, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
        Synthesizer.Speak(speechMessage.Text);
    
        byte[] buffer = memoryStream.ToArray();

        using WaveStream waveStream = new RawSourceWaveStream(buffer, 0, buffer.Length, OutputFormat);
        ISampleProvider sample = waveStream.ToSampleProvider();
        SampleProviders.Add(sample);
        if (!_isPlaying)
        {
            ProcessQueue();
        }
    }

    private static void AddMixerInput(ISampleProvider input)
    {
        Mixer.AddMixerInput(input);
    }

    public void Dispose()
    {
        OutputDevice.Dispose();
    }
}