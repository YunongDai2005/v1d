using UnityEngine;

public class ProceduralSfxLibrary : MonoBehaviour
{
    public enum SfxStyle
    {
        Underwater,
        Arcade
    }

    [Header("Style")]
    [SerializeField] private SfxStyle style = SfxStyle.Underwater;

    [Header("Synthesis")]
    [SerializeField] private int sampleRate = 44100;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;

    [Header("Generated Clips (Read Only)")]
    public AudioClip shootClip;
    public AudioClip hitClip;
    public AudioClip enemyDeadClip;
    public AudioClip playerHurtClip;
    public AudioClip pickupClip;
    public AudioClip dashClip;
    public AudioClip warningClip;
    public AudioClip teleportClip;
    public AudioClip spawnClip;
    public AudioClip uiConfirmClip;

    private void Awake()
    {
        RegenerateAll();
    }

    [ContextMenu("Regenerate Procedural SFX")]
    public void RegenerateAll()
    {
        if (style == SfxStyle.Underwater)
        {
            GenerateUnderwaterSet();
        }
        else
        {
            GenerateArcadeSet();
        }
    }

    private void GenerateUnderwaterSet()
    {
        // More natural underwater palette: filtered noise + damped sine resonances.
        shootClip = PostProcess(CreateUnderwaterShot(0.12f), 2400f, 0.03f);
        hitClip = PostProcess(CreateUnderwaterImpact(0.16f, 140f), 1600f, 0.01f);
        enemyDeadClip = PostProcess(CreateUnderwaterImpact(0.40f, 85f), 1200f, 0.08f);
        playerHurtClip = PostProcess(CreateUnderwaterImpact(0.24f, 110f), 1300f, 0.04f);
        pickupClip = PostProcess(CreateResonantPing(0.17f, 420f, 610f, 4.5f, 0.03f), 3200f, 0.01f);
        dashClip = PostProcess(CreateBubbleWhoosh(0.24f), 1800f, 0.04f);
        warningClip = PostProcess(CreateSonarPulse(0.55f, 260f, 2.5f), 1300f, 0.10f);
        teleportClip = PostProcess(CreateResonantSweep(0.58f, 140f, 980f), 2500f, 0.16f);
        spawnClip = PostProcess(CreateResonantSweep(0.28f, 180f, 520f), 2100f, 0.05f);
        uiConfirmClip = PostProcess(CreateResonantPing(0.14f, 520f, 760f, 5.0f, 0.01f), 3600f, 0f);
    }

    private void GenerateArcadeSet()
    {
        shootClip = PostProcess(CreateChirp("arc_shoot", 0.1f, 1200f, 360f, 0.15f, WaveType.Square, 0.02f), 9000f, 0f);
        hitClip = PostProcess(CreateNoiseBurst("arc_hit", 0.12f, 0.35f, 0.01f), 8000f, 0f);
        enemyDeadClip = PostProcess(CreateChirp("arc_enemy_dead", 0.3f, 440f, 110f, 0.25f, WaveType.Saw, 0.03f), 7000f, 0f);
        playerHurtClip = PostProcess(CreateChirp("arc_player_hurt", 0.18f, 320f, 120f, 0.2f, WaveType.Square, 0.02f), 7000f, 0f);
        pickupClip = PostProcess(CreateDoubleTone("arc_pickup", 0.16f, 700f, 980f, 0.02f, WaveType.Sine, 0.02f), 10000f, 0f);
        dashClip = PostProcess(CreateNoiseBurst("arc_dash", 0.15f, 0.18f, 0.01f), 8500f, 0f);
        warningClip = PostProcess(CreatePulse("arc_warning", 0.4f, 500f, 4f, 0.02f), 9000f, 0f);
        teleportClip = PostProcess(CreateChirp("arc_teleport", 0.45f, 260f, 1300f, 0.22f, WaveType.Sine, 0.04f), 9500f, 0.08f);
        spawnClip = PostProcess(CreateChirp("arc_spawn", 0.22f, 260f, 620f, 0.25f, WaveType.Triangle, 0.03f), 8500f, 0f);
        uiConfirmClip = PostProcess(CreateDoubleTone("arc_ui_confirm", 0.14f, 820f, 1100f, 0.015f, WaveType.Sine, 0.01f), 10000f, 0f);
    }

    private AudioClip PostProcess(float[] data, float lowPassCutoffHz, float echoMix)
    {
        if (lowPassCutoffHz > 0f)
        {
            ApplyLowPass(data, lowPassCutoffHz);
        }
        if (echoMix > 0f)
        {
            ApplyEcho(data, 0.08f, echoMix);
        }
        SoftClip(data, 1.6f);
        Normalize(data, 0.72f);
        return BuildClip("proc", data);
    }

    private float[] CreateChirp(string name, float lengthSec, float startFreq, float endFreq, float noiseMix, WaveType wave, float attackSec)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += (2f * Mathf.PI * freq) / sampleRate;
            float env = ADSREnvelope(t, attackSec / lengthSec, 0.1f, 0.6f, 0.24f);
            float tone = WaveSample(wave, phase);
            float noise = (Random.value * 2f - 1f) * noiseMix;
            data[i] = (tone * (1f - noiseMix) + noise) * env * masterVolume;
        }
        return data;
    }

    private float[] CreateUnderwaterShot(float lengthSec)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phase = 0f;
        float lp = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float freq = Mathf.Lerp(720f, 220f, t);
            phase += (2f * Mathf.PI * freq) / sampleRate;

            float env = Mathf.Exp(-10f * t);
            float tonal = Mathf.Sin(phase) * 0.75f;

            float n = Random.value * 2f - 1f;
            float alpha = Mathf.Lerp(0.45f, 0.18f, t); // softer tail
            lp += (n - lp) * alpha;

            data[i] = (tonal + lp * 0.35f) * env * masterVolume;
        }

        return data;
    }

    private float[] CreateUnderwaterImpact(float lengthSec, float bodyFreq)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phase = 0f;
        float lp = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            phase += (2f * Mathf.PI * bodyFreq) / sampleRate;

            float thumpEnv = Mathf.Exp(-6.5f * t);
            float noiseEnv = Mathf.Exp(-12f * t);

            float thump = Mathf.Sin(phase) * thumpEnv;
            float n = Random.value * 2f - 1f;
            lp += (n - lp) * 0.2f;
            float debris = lp * noiseEnv * 0.55f;

            data[i] = (thump + debris) * masterVolume;
        }

        return data;
    }

    private float[] CreateWhoosh(float lengthSec, float startCutoff, float endCutoff)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float low = 0f;
        float high = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float n = Random.value * 2f - 1f;

            float cutoff = Mathf.Lerp(startCutoff, endCutoff, t);
            float lpAlpha = CutoffToAlpha(cutoff);
            low += lpAlpha * (n - low);

            // simple high-pass from low-pass residual
            high = n - low;

            float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); // rise/fall
            data[i] = high * env * 0.65f * masterVolume;
        }

        return data;
    }

    private float[] CreateBubbleWhoosh(float lengthSec)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];

        float low = 0f;
        float phaseA = 0f;
        float phaseB = 0f;
        float bubbleTick = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float n = Random.value * 2f - 1f;

            // soft water rush body (band-limited noise)
            float cutoff = Mathf.Lerp(1700f, 550f, t);
            float lpAlpha = CutoffToAlpha(cutoff);
            low += lpAlpha * (n - low);
            float rush = (n - low) * 0.45f;

            // rising bubble resonances
            float fA = Mathf.Lerp(220f, 420f, t);
            float fB = Mathf.Lerp(340f, 760f, t);
            phaseA += (2f * Mathf.PI * fA) / sampleRate;
            phaseB += (2f * Mathf.PI * fB) / sampleRate;
            float bubbles = Mathf.Sin(phaseA) * 0.26f + Mathf.Sin(phaseB) * 0.18f;

            // tiny pop modulation for bubble texture
            bubbleTick += TimeStepPulse(t, 18f, 36f);
            float pops = Mathf.Sin(bubbleTick) * 0.08f;

            float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); // smooth in/out
            data[i] = (rush + bubbles + pops) * env * 0.75f * masterVolume;
        }

        return data;
    }

    private float TimeStepPulse(float t, float f0, float f1)
    {
        float f = Mathf.Lerp(f0, f1, t);
        return (2f * Mathf.PI * f) / sampleRate;
    }

    private float[] CreateSonarPulse(float lengthSec, float freq, float pulseHz)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phase = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float gate = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * pulseHz * t)), 2.2f);
            float env = Mathf.Exp(-1.6f * t);
            phase += (2f * Mathf.PI * freq) / sampleRate;
            float tone = Mathf.Sin(phase);
            data[i] = tone * gate * env * 0.75f * masterVolume;
        }

        return data;
    }

    private float[] CreateResonantSweep(float lengthSec, float f0, float f1)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phaseA = 0f;
        float phaseB = 0f;
        float lp = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float f = Mathf.Lerp(f0, f1, t);
            phaseA += (2f * Mathf.PI * f) / sampleRate;
            phaseB += (2f * Mathf.PI * (f * 0.51f)) / sampleRate;

            float env = ADSREnvelope(t, 0.06f, 0.18f, 0.65f, 0.28f);
            float tone = Mathf.Sin(phaseA) * 0.7f + Mathf.Sin(phaseB) * 0.3f;

            float n = Random.value * 2f - 1f;
            lp += (n - lp) * 0.1f;
            data[i] = (tone + lp * 0.12f) * env * masterVolume;
        }

        return data;
    }

    private float[] CreateResonantPing(float lengthSec, float fA, float fB, float decay, float noiseMix)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phaseA = 0f;
        float phaseB = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            phaseA += (2f * Mathf.PI * fA) / sampleRate;
            phaseB += (2f * Mathf.PI * fB) / sampleRate;

            float env = Mathf.Exp(-decay * t);
            float tone = Mathf.Sin(phaseA) * 0.65f + Mathf.Sin(phaseB) * 0.35f;
            float noise = (Random.value * 2f - 1f) * noiseMix;
            data[i] = (tone + noise) * env * masterVolume;
        }

        return data;
    }

    private float CutoffToAlpha(float cutoffHz)
    {
        cutoffHz = Mathf.Max(1f, cutoffHz);
        float rc = 1f / (2f * Mathf.PI * cutoffHz);
        float dt = 1f / sampleRate;
        return dt / (rc + dt);
    }

    private float[] CreateNoiseBurst(string name, float lengthSec, float lowPassBlend, float attackSec)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float prev = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float env = ADSREnvelope(t, attackSec / lengthSec, 0.05f, 0.45f, 0.3f);
            float n = Random.value * 2f - 1f;
            prev = Mathf.Lerp(n, prev, lowPassBlend);
            data[i] = prev * env * masterVolume;
        }
        return data;
    }

    private float[] CreatePulse(string name, float lengthSec, float freq, float pulseRate, float attackSec)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float env = ADSREnvelope(t, attackSec / lengthSec, 0.08f, 0.8f, 0.12f);
            float gate = Mathf.Sin(2f * Mathf.PI * pulseRate * t) > 0f ? 1f : 0.25f;
            phase += (2f * Mathf.PI * freq) / sampleRate;
            data[i] = Mathf.Sin(phase) * gate * env * masterVolume;
        }
        return data;
    }

    private float[] CreateDoubleTone(string name, float lengthSec, float freqA, float freqB, float splitSec, WaveType wave, float attackSec)
    {
        int length = Mathf.Max(1, Mathf.RoundToInt(lengthSec * sampleRate));
        float[] data = new float[length];
        float phaseA = 0f;
        float phaseB = 0f;
        int split = Mathf.Clamp(Mathf.RoundToInt(splitSec * sampleRate), 1, length - 1);
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / (length - 1);
            float env = ADSREnvelope(t, attackSec / lengthSec, 0.08f, 0.7f, 0.2f);
            if (i < split)
            {
                phaseA += (2f * Mathf.PI * freqA) / sampleRate;
                data[i] = WaveSample(wave, phaseA) * env * masterVolume;
            }
            else
            {
                phaseB += (2f * Mathf.PI * freqB) / sampleRate;
                data[i] = WaveSample(wave, phaseB) * env * masterVolume;
            }
        }
        return data;
    }

    private AudioClip BuildClip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void ApplyLowPass(float[] data, float cutoffHz)
    {
        float rc = 1f / (2f * Mathf.PI * cutoffHz);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        float y = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            y += alpha * (data[i] - y);
            data[i] = y;
        }
    }

    private void ApplyEcho(float[] data, float delaySec, float mix)
    {
        int delay = Mathf.Max(1, Mathf.RoundToInt(delaySec * sampleRate));
        for (int i = delay; i < data.Length; i++)
        {
            data[i] += data[i - delay] * mix;
        }
    }

    private void Normalize(float[] data, float maxPeak)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float a = Mathf.Abs(data[i]);
            if (a > peak) peak = a;
        }
        if (peak < 1e-5f) return;
        float k = maxPeak / peak;
        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= k;
        }
    }

    private void SoftClip(float[] data, float drive)
    {
        drive = Mathf.Max(0.01f, drive);
        for (int i = 0; i < data.Length; i++)
        {
            float x = data[i] * drive;
            data[i] = (float)(System.Math.Tanh(x) / System.Math.Tanh(drive));
        }
    }

    private float ADSREnvelope(float t01, float a, float d, float s, float r)
    {
        a = Mathf.Max(0.0001f, a);
        d = Mathf.Max(0.0001f, d);
        r = Mathf.Max(0.0001f, r);
        float sustainLevel = Mathf.Clamp01(s);
        if (t01 < a) return t01 / a;
        if (t01 < a + d)
        {
            float td = (t01 - a) / d;
            return Mathf.Lerp(1f, sustainLevel, td);
        }
        if (t01 < 1f - r) return sustainLevel;
        float tr = (t01 - (1f - r)) / r;
        return Mathf.Lerp(sustainLevel, 0f, tr);
    }

    private enum WaveType
    {
        Sine,
        Square,
        Triangle,
        Saw
    }

    private float WaveSample(WaveType wave, float phase)
    {
        switch (wave)
        {
            case WaveType.Square: return Mathf.Sin(phase) >= 0f ? 1f : -1f;
            case WaveType.Triangle: return Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f;
            case WaveType.Saw: return Mathf.Repeat(phase / (2f * Mathf.PI), 1f) * 2f - 1f;
            default: return Mathf.Sin(phase);
        }
    }
}
