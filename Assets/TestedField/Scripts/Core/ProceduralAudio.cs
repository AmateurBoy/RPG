using UnityEngine;

namespace TestedField
{
    /// <summary>Звуки-заглушки, сгенерированные в коде (аналог S_Hit_Placeholder).</summary>
    public static class ProceduralAudio
    {
        public static AudioClip MakeHit()
        {
            const int sampleRate = 44100;
            const float length = 0.12f;
            int samples = Mathf.CeilToInt(sampleRate * length);
            var data = new float[samples];
            var rng = new RandomStream(1337);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 38f);
                float noise = rng.GetFraction() * 2f - 1f;
                float thump = Mathf.Sin(2f * Mathf.PI * 140f * t);
                data[i] = (noise * 0.55f + thump * 0.45f) * envelope * 0.8f;
            }

            var clip = AudioClip.Create("S_Hit_Placeholder", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip MakeCast(float pitch)
        {
            const int sampleRate = 44100;
            const float length = 0.25f;
            int samples = Mathf.CeilToInt(sampleRate * length);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t / length);
                float freq = Mathf.Lerp(300f, 900f, t / length) * pitch;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.35f;
            }

            var clip = AudioClip.Create("S_Cast_Placeholder", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
