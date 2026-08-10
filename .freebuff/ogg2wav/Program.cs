using NVorbis;

if (args.Length < 2)
{
    Console.WriteLine("Usage: ogg2wav <input.ogg> <output.wav>");
    return 1;
}

var input = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);

using var stream = File.OpenRead(input);
using var vorbis = new VorbisReader(stream, false);
vorbis.Initialize();

var sampleRate = vorbis.SampleRate;
var channels = vorbis.Channels;
var totalSamples = vorbis.TotalSamples;

Console.WriteLine($"Input: {sampleRate} Hz, {channels} ch, {totalSamples} frames");

// Read all frames interleaved, then mix down to mono.
// VorbisPizza ReadSamples returns the number of FRAMES read (like Robust's loader).
var readBuffer = new float[4096 * channels];
var mono = new float[totalSamples];
long frame = 0;

while (frame < totalSamples)
{
    var read = vorbis.ReadSamples(readBuffer);
    if (read <= 0)
        break;

    var count = read * channels;
    for (var i = 0; i < read; i++)
    {
        var sum = 0f;
        for (var c = 0; c < channels; c++)
            sum += readBuffer[i * channels + c];
        mono[frame + i] = sum / channels;
    }

    frame += read;
}

var frames = frame;
Console.WriteLine($"Mono: {frames} frames -> {frames / (double) sampleRate:F2}s");

// Write 16-bit PCM WAV (mono).
using var wav = new BinaryWriter(File.Create(output));
var dataSize = (int) frames * 2;
var byteRate = sampleRate * 2;

wav.Write("RIFF"u8);
wav.Write(36 + dataSize);
wav.Write("WAVE"u8);
wav.Write("fmt "u8);
wav.Write(16);
wav.Write((short) 1); // PCM
wav.Write((short) 1); // mono
wav.Write(sampleRate);
wav.Write(byteRate);
wav.Write((short) 2); // block align
wav.Write((short) 16); // bits per sample
wav.Write("data"u8);
wav.Write(dataSize);

for (var i = 0; i < frames; i++)
{
    var s = Math.Clamp(mono[i], -1f, 1f);
    wav.Write((short) (s * short.MaxValue));
}

Console.WriteLine($"Wrote {output} ({dataSize + 44} bytes)");
return 0;
