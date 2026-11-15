using System.Collections.Generic;
using UnityEngine;

// Musik latar & efek suara. Semua suara dibuat secara prosedural (tanpa file audio),
// volumenya mengikuti GameSettings (Utama, Musik, Efek Suara).
public class AudioManager : MonoBehaviour
{
    static AudioManager inst;
    public static AudioManager Instance
    {
        get
        {
            if (inst == null)
            {
                if (quitting || !Application.isPlaying) return null;
                var go = new GameObject("AudioManager");
                inst = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return inst;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; quitting = false; Application.quitting -= OnQuit; Application.quitting += OnQuit; }
    static bool quitting;
    static void OnQuit() { quitting = true; }

    const int SR = 22050;
    AudioSource music, sfx, loop;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    string currentMusic;

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false;
        sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false;
        loop = gameObject.AddComponent<AudioSource>(); loop.loop = true; loop.playOnAwake = false;
        amb = gameObject.AddComponent<AudioSource>(); amb.loop = true; amb.playOnAwake = false;
        ApplyVolumes();
    }

    void OnEnable() { GameSettings.Changed += ApplyVolumes; }
    void OnDisable() { GameSettings.Changed -= ApplyVolumes; }

    void ApplyVolumes()
    {
        if (music != null) music.volume = 0.55f * GameSettings.MusicVolume;
        if (loop != null) loop.volume = 0.35f * GameSettings.SfxVolume;
        if (amb != null) amb.volume = 0.22f * GameSettings.SfxVolume;
    }

    // ---------------- API ----------------
    public static void PlayMusic(string name)
    {
        var a = Instance;
        if (a == null) return;
        if (a.currentMusic == name && a.music.isPlaying) return;
        a.currentMusic = name;
        a.music.clip = a.Get(name);
        a.ApplyVolumes();
        a.music.Play();
    }

    public static void Sfx(string name, float vol = 1f)
    {
        var a = Instance;
        if (a == null) return;
        a.sfx.PlayOneShot(a.Get(name), vol * GameSettings.SfxVolume);
    }

    AudioSource amb; string currentAmb;

    // suasana latar: angin + burung (siang) atau jangkrik (malam). null = matikan
    public static void Ambience(string name)
    {
        var a = Instance; if (a == null) return;
        if (string.IsNullOrEmpty(name)) { a.amb.Stop(); a.currentAmb = null; return; }
        if (a.currentAmb == name && a.amb.isPlaying) return;
        a.currentAmb = name; a.amb.clip = a.Get(name); a.ApplyVolumes(); a.amb.Play();
    }

    public static void Siren(bool on)
    {
        if (!on && inst == null) return;
        var a = Instance;
        if (a == null) return;
        if (on) { if (!a.loop.isPlaying) { a.loop.clip = a.Get("siren"); a.ApplyVolumes(); a.loop.Play(); } }
        else a.loop.Stop();
    }

    AudioClip Get(string n)
    {
        AudioClip c;
        if (clips.TryGetValue(n, out c)) return c;
        float[] d;
        switch (n)
        {
            case "music_city": d = MakeMusic(96f, true); break;
            case "music_menu": d = MakeMusic(78f, false); break;
            case "pickup": d = Blips(new[] { 660f, 990f }, 0.07f, 0.35f); break;
            case "complete": d = Blips(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.5f); break;
            case "caught": d = Sweep(420f, 130f, 0.7f); break;
            case "click": d = Tone(1200f, 0.04f, 0.25f, 60f); break;
            case "blip": d = Tone(820f, 0.035f, 0.18f, 70f); break;
            case "siren": d = MakeSiren(); break;
            case "amb_day": d = MakeAmbience(false); break;
            case "amb_night": d = MakeAmbience(true); break;
            case "step": d = MakeStep(); break;
            default: d = new float[SR / 10]; break;
        }
        c = AudioClip.Create(n, d.Length, 1, SR, false);
        c.SetData(d, 0);
        clips[n] = c;
        return c;
    }

    // ---------------- synth ----------------
    static float Midi(int m) { return 440f * Mathf.Pow(2f, (m - 69) / 12f); }

    static void AddNote(float[] buf, float start, float dur, float freq, float amp, float decay, int harmonics)
    {
        int s0 = (int)(start * SR), n = (int)(dur * SR);
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * decay);
            float v = Mathf.Sin(2f * Mathf.PI * freq * t);
            if (harmonics > 0) v += 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 4f * t) * Mathf.Exp(-t * decay * 3f);
            if (harmonics > 1) v += 0.2f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
            buf[s0 + i] += v * env * amp;
        }
    }

    static void AddPad(float[] buf, float start, float dur, float freq, float amp)
    {
        int s0 = (int)(start * SR), n = (int)(dur * SR);
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Min(1f, t * 3f) * Mathf.Min(1f, (dur - t) * 3f);
            float v = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 1.005f * t);
            buf[s0 + i] += v * env * amp;
        }
    }

    static void AddKick(float[] buf, float start)
    {
        int s0 = (int)(start * SR), n = (int)(0.25f * SR); float ph = 0f;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float t = i / (float)SR;
            float f = Mathf.Lerp(120f, 45f, Mathf.Min(1f, t * 8f));
            ph += 2f * Mathf.PI * f / SR;
            buf[s0 + i] += Mathf.Sin(ph) * Mathf.Exp(-t * 14f) * 0.55f;
        }
    }

    static void AddNoise(float[] buf, float start, float dur, float amp, float decay, System.Random r)
    {
        int s0 = (int)(start * SR), n = (int)(dur * SR); float prev = 0f;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float t = i / (float)SR;
            float w = (float)(r.NextDouble() * 2 - 1);
            float hp = w - prev; prev = w; // high-pass sederhana
            buf[s0 + i] += hp * Mathf.Exp(-t * decay) * amp;
        }
    }

    static float[] MakeMusic(float bpm, bool drums)
    {
        float beat = 60f / bpm, bar = beat * 4f;
        int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 55, 59, 62 } }; // Am F C G
        int bars = 8;
        var buf = new float[(int)(bar * bars * SR)];
        var rnd = new System.Random(3);
        for (int b = 0; b < bars; b++)
        {
            var ch = chords[b % 4];
            float t0 = b * bar;
            foreach (var m in ch) AddPad(buf, t0, bar, Midi(m), drums ? 0.05f : 0.07f);
            AddNote(buf, t0, beat * 1.8f, Midi(ch[0] - 12), 0.28f, 3f, 2);
            AddNote(buf, t0 + beat * 2f, beat * 1.8f, Midi(ch[0] - 12), 0.24f, 3f, 2);
            // marimba arpeggio (pola tropis)
            int steps = drums ? 8 : 4;
            for (int s = 0; s < steps; s++)
            {
                if (drums && (s == 3 || s == 6) && b % 2 == 1) continue;
                int note = ch[(s * 2 + b) % 3] + ((s % 4 == 2) ? 12 : 0);
                float st = t0 + s * (bar / steps);
                AddNote(buf, st, 0.6f, Midi(note + 12), drums ? 0.13f : 0.11f, 7f, 1);
            }
            if (drums)
            {
                for (int k = 0; k < 4; k++)
                {
                    float bt = t0 + k * beat;
                    if (k == 0 || k == 2) AddKick(buf, bt);
                    if (k == 1 || k == 3) AddNoise(buf, bt, 0.18f, 0.22f, 22f, rnd);
                    AddNoise(buf, bt + beat * 0.5f, 0.05f, 0.08f, 60f, rnd);
                    AddNoise(buf, bt, 0.05f, 0.05f, 60f, rnd);
                }
            }
        }
        Normalize(buf, 0.8f);
        return buf;
    }

    static float[] Blips(float[] freqs, float each, float amp)
    {
        var buf = new float[(int)((freqs.Length * each + 0.35f) * SR)];
        for (int i = 0; i < freqs.Length; i++) AddNote(buf, i * each, 0.35f, freqs[i], amp, 9f, 1);
        Normalize(buf, 0.7f);
        return buf;
    }

    static float[] Tone(float f, float dur, float amp, float decay)
    {
        var buf = new float[(int)(dur * SR) + 200];
        AddNote(buf, 0f, dur, f, amp, decay, 0);
        return buf;
    }

    static float[] Sweep(float f0, float f1, float dur)
    {
        var buf = new float[(int)(dur * SR)]; float ph = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SR, k = t / dur;
            ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, k) / SR;
            float sq = Mathf.Sign(Mathf.Sin(ph)) * 0.5f + Mathf.Sin(ph) * 0.5f;
            buf[i] = sq * (1f - k) * 0.4f;
        }
        return buf;
    }

    static float[] MakeSiren()
    {
        float dur = 1.4f;
        var buf = new float[(int)(dur * SR)]; float ph = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SR;
            float f = t < dur / 2f ? 720f : 960f;
            ph += 2f * Mathf.PI * f / SR;
            float tri = 2f * Mathf.Abs(2f * (ph / (2f * Mathf.PI) - Mathf.Floor(ph / (2f * Mathf.PI) + 0.5f))) - 1f;
            float edge = Mathf.Min(1f, Mathf.Min(t, Mathf.Abs(t - dur / 2f), dur - t) * 60f);
            buf[i] = tri * 0.35f * (0.4f + 0.6f * edge);
        }
        return buf;
    }

    static float[] MakeAmbience(bool night)
    {
        int n = SR * 8; var b = new float[n]; var r = new System.Random(night ? 7 : 3);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float w = (float)(r.NextDouble() * 2 - 1);
            lp += (w - lp) * 0.02f; lp2 += (lp - lp2) * 0.05f;      // angin lembut
            float gust = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 8f);
            // loop mulus: fade di awal & akhir 0.25 detik
            b[i] = lp2 * 6f * gust * (night ? 0.6f : 1f);
        }
        if (!night)
        {
            for (int k = 0; k < 9; k++) // kicauan burung
            {
                float st = (float)r.NextDouble() * 7.2f, f0 = 2400f + (float)r.NextDouble() * 1800f;
                int notes = 2 + r.Next(4);
                for (int q = 0; q < notes; q++)
                {
                    int s0 = (int)((st + q * 0.11f) * SR), len = (int)(0.08f * SR);
                    for (int j = 0; j < len && s0 + j < n; j++)
                    {
                        float tt = j / (float)SR; float env = Mathf.Sin(Mathf.PI * j / len);
                        b[s0 + j] += Mathf.Sin(2f * Mathf.PI * (f0 + 900f * tt / 0.08f * (q % 2 == 0 ? 1f : -1f)) * tt) * env * 0.12f;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < n; i++) // jangkrik
            {
                float t = i / (float)SR;
                float chirp = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2.8f * t)) ; chirp = chirp > 0.6f ? 1f : 0f;
                float pulse = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 32f * t));
                b[i] += Mathf.Sin(2f * Mathf.PI * 4300f * t) * chirp * pulse * 0.05f;
                float chirp2 = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 2.1f * t + 1.3f)) > 0.7f ? 1f : 0f;
                b[i] += Mathf.Sin(2f * Mathf.PI * 3900f * t) * chirp2 * pulse * 0.03f;
            }
        }
        int fade = SR / 4;
        for (int i = 0; i < fade; i++) { float k = i / (float)fade; b[i] *= k; b[n - 1 - i] *= k; }
        Normalize(b, 0.5f);
        return b;
    }

    static float[] MakeStep()
    {
        int n = (int)(0.09f * SR); var b = new float[n]; var r = new System.Random(5); float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.25f;
            b[i] = (lp * 0.8f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.6f) * Mathf.Exp(-t * 55f);
        }
        Normalize(b, 0.6f);
        return b;
    }

    static void Normalize(float[] b, float peak)
    {
        float m = 0f; foreach (var v in b) m = Mathf.Max(m, Mathf.Abs(v));
        if (m < 1e-5f) return;
        float k = peak / m;
        for (int i = 0; i < b.Length; i++) b[i] *= k;
    }
}

