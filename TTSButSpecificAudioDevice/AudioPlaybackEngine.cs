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
    private static readonly SpeechSynthesizer Synthesizer = new();
    
    private static readonly WaveFormat OutputFormat = new(22050, 1);
    private static readonly WaveFormat IeeeFloatWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(OutputFormat.SampleRate, OutputFormat.Channels);
    private static readonly SpeechAudioFormatInfo SpeechFormat = new(OutputFormat.SampleRate, AudioBitsPerSample.Sixteen, (AudioChannel)OutputFormat.Channels);

    private static readonly Dictionary<string, string> Voices = [];

    private static readonly List<ISampleProvider> SampleProviders = [];
    private static bool _isPlaying;
    
    private static string _defaultVoice = string.Empty;
    
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
            
            if (string.IsNullOrEmpty(_defaultVoice))
            {
                _defaultVoice = name;
            }
        }
    }

    static AudioPlaybackEngine()
    {
        InitVoices();
        
        Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"))!;
    
        bool tryParse = Guid.TryParse(config.AudioDeviceGuid, out Guid guid);
        if (!tryParse)
        {
            goto invalidAudioDevice;
        }
        // ReSharper disable once SimplifyLinqExpressionUseAll (wtf, isn't this the same???)
        if (!DirectSoundOut.Devices.Any(x => x.Guid == guid))
        {
            goto invalidAudioDevice;
        }
        
        OutputDevice = new DirectSoundOut(guid);
        Mixer = new MixingSampleProvider(IeeeFloatWaveFormat)
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
        return;
        
        invalidAudioDevice:
            Console.WriteLine($"Audio device {config.AudioDeviceGuid} not found, please use one of the GUIDs below:");
            ListAudioDevices();
            throw new Exception("Invalid audio device");
    }

    private static void ProcessQueue()
    {
        if (SampleProviders.Count == 0 || _isPlaying)
        {
            return;
        }
        
        _isPlaying = true;
        
        try
        {
            Mixer.AddMixerInput(SampleProviders[0]);
            SampleProviders.Remove(SampleProviders[0]);
        }
        catch (Exception)
        {
            _isPlaying = false;
        }
    }

    public static void AddMessageToQueue(SpeechMessage speechMessage)
    {
        using MemoryStream memoryStream = new();
        
        Synthesizer.SelectVoice(Voices[speechMessage.Voice ?? _defaultVoice]);
        Synthesizer.Rate = (int)speechMessage.Rate;
        Synthesizer.SetOutputToAudioStream(memoryStream, SpeechFormat);
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

    public void Dispose()
    {
        OutputDevice.Dispose();
    }
}