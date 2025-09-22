This is a simple HTTP server that listens for POST requests that fires off Windows-native TTS events (`System.Speech.Synthesis`) directed towards a user-defined audio device. ~~What a mouthful.~~

## Setup
- The listen address/port for the HTTP server can be changed in `config.json`, by default it will listen on `127.0.0.1:6968`.
- If the audio device you set in `config.json` cannot be found, or if it's left blank, the program will list out GUIDs for each audio device. Find the one you want to use, and copy its GUID to `config.json`.
```json
{
  "HttpAddress": "127.0.0.1",
  "HttpPort": 6968,
  "AudioDeviceGuid": "01234567-89ab-cdef-0123-456789abcdef"
}
```
- The voices listed out on program startup are the system's available voices, along with the associated region.
  - To add or remove voices, check *Installed voice packages* in your Speech settings.

## Usage
Send out an HTTP POST request to the address and port the program is listening on (`http://127.0.0.1:6968` by default).

The program expects data to be JSON formatted:
```json
{
  "voice": "Mark",
  "text": "Hello, world!",
  "rate": 0
}
```
`voice` and `text` are the only required keys. `rate` controls the speed of the synthesized speech output, with `0` being the default speed.