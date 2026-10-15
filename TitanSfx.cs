using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TitansMod
{
    // Sons reais de titã (extraídos do AoTTG2 em WAV) carregados como AudioClip em runtime.
    public class TitanSfx
    {
        public static TitanSfx Loaded;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, List<AudioClip>> groups = new Dictionary<string, List<AudioClip>>();
        private readonly System.Random rng = new System.Random();
        public int Count { get { return clips.Count; } }

        public static bool TryLoad(string dir, Action<string> log)
        {
            try
            {
                if (!Directory.Exists(dir)) { log("sfx: pasta não achada " + dir); return false; }
                var s = new TitanSfx();
                foreach (var f in Directory.GetFiles(dir, "*.wav"))
                {
                    try
                    {
                        var clip = LoadWav(f);
                        if (clip == null) continue;
                        string name = Path.GetFileNameWithoutExtension(f);
                        s.clips[name] = clip;
                        string g = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                        List<AudioClip> list;
                        if (!s.groups.TryGetValue(g, out list)) { list = new List<AudioClip>(); s.groups[g] = list; }
                        list.Add(clip);
                    }
                    catch (Exception e) { log("sfx " + Path.GetFileName(f) + ": " + e.Message); }
                }
                Loaded = s;
                log("sfx: " + s.clips.Count + " sons de titã carregados (" + string.Join(",", new List<string>(s.groups.Keys).ToArray()) + ")");
                return s.clips.Count > 0;
            }
            catch (Exception e) { log("sfx falhou: " + e.Message); return false; }
        }

        public AudioClip Get(string group)
        {
            List<AudioClip> list;
            if (groups.TryGetValue(group, out list) && list.Count > 0) return list[rng.Next(list.Count)];
            AudioClip c;
            return clips.TryGetValue(group, out c) ? c : null;
        }

        // WAV PCM 16-bit / 24-bit / float32 -> AudioClip
        private static AudioClip LoadWav(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;
            int pos = 12;
            int channels = 1, rate = 44100, bits = 16, fmt = 1;
            int dataOff = -1, dataLen = 0;
            while (pos + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
                int len = BitConverter.ToInt32(b, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    fmt = BitConverter.ToInt16(b, body);
                    channels = BitConverter.ToInt16(b, body + 2);
                    rate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToInt16(b, body + 14);
                    if (fmt == 0xFFFE && len >= 26) fmt = BitConverter.ToInt16(b, body + 24); // WAVE_FORMAT_EXTENSIBLE
                }
                else if (id == "data") { dataOff = body; dataLen = Math.Min(len, b.Length - body); break; }
                pos = body + len + (len & 1);
            }
            if (dataOff < 0 || channels <= 0) return null;
            int bytesPer = bits / 8;
            int frames = dataLen / (bytesPer * channels);
            var samples = new float[frames * channels];
            int p = dataOff;
            for (int i = 0; i < samples.Length; i++)
            {
                float v;
                if (fmt == 3 && bits == 32) v = BitConverter.ToSingle(b, p);
                else if (bits == 16) v = BitConverter.ToInt16(b, p) / 32768f;
                else if (bits == 24) { int x = (b[p] | (b[p + 1] << 8) | (b[p + 2] << 16)); if ((x & 0x800000) != 0) x |= unchecked((int)0xFF000000); v = x / 8388608f; }
                else if (bits == 8) v = (b[p] - 128) / 128f;
                else if (bits == 32) v = BitConverter.ToInt32(b, p) / 2147483648f;
                else v = 0f;
                samples[i] = v;
                p += bytesPer;
            }
            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), frames, channels, rate, false);
            clip.SetData(samples, 0);
            try { clip.hideFlags = HideFlags.DontUnloadUnusedAsset; } catch { }
            return clip;
        }
    }
}
