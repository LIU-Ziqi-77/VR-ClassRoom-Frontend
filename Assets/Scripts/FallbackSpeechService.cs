using UnityEngine;
using System.Collections;
using VRM;

/// <summary>
/// Provides a demo-quality "speaking" experience without Azure TTS dependency.
/// Generates procedural audio tone + drives VRM BlendShape mouth movement.
/// Attach to each student avatar alongside ProceduralBehaviorAnimator.
///
/// TEMPORARY FRONTEND PLACEHOLDER — replace with real TTS when Azure key is available.
/// </summary>
public class FallbackSpeechService : MonoBehaviour
{
    [Header("References")]
    public VRMBlendShapeProxy blendShapeProxy;
    public AudioSource audioSource;
    public ProceduralBehaviorAnimator proceduralAnimator;

    [Header("Audio Settings")]
    [Range(120, 300)] public float baseFrequency = 180f;
    [Range(0.05f, 0.5f)] public float volume = 0.15f;
    public bool generateAudio = true;

    [Header("State")]
    public bool isSpeaking;

    private Coroutine _speechRoutine;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.loop = false;

        if (blendShapeProxy == null)
            blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
        if (proceduralAnimator == null)
            proceduralAnimator = GetComponent<ProceduralBehaviorAnimator>();
    }

    /// <summary>
    /// Start speaking with fallback audio + lip sync.
    /// Duration is estimated from text length if not specified.
    /// </summary>
    public void Speak(string text, float duration = 0)
    {
        if (isSpeaking) return;
        if (duration <= 0) duration = Mathf.Max(1.5f, text.Length * 0.08f);
        _speechRoutine = StartCoroutine(SpeechRoutine(text, duration));
    }

    public void StopSpeaking()
    {
        if (_speechRoutine != null)
        {
            StopCoroutine(_speechRoutine);
            _speechRoutine = null;
        }
        isSpeaking = false;
        ResetMouth();
        if (audioSource.isPlaying) audioSource.Stop();
    }

    IEnumerator SpeechRoutine(string text, float duration)
    {
        isSpeaking = true;
        Debug.Log($"[FallbackSpeech] {gameObject.name}: \"{text}\" ({duration:F1}s)");

        if (generateAudio)
        {
            AudioClip clip = GenerateProceduralSpeechClip(duration);
            audioSource.clip = clip;
            audioSource.Play();
        }

        if (proceduralAnimator != null)
            proceduralAnimator.PlaySpeakingMotion(duration);

        float elapsed = 0;
        while (elapsed < duration)
        {
            DriveLipSync(elapsed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetMouth();
        isSpeaking = false;
        _speechRoutine = null;
    }

    void DriveLipSync(float t)
    {
        if (blendShapeProxy == null) return;

        // Procedural vowel cycling: A → I → U → E → O
        float cycle = t * 4f;
        float a = Mathf.Clamp01(Mathf.Sin(cycle * 2.1f + 0.0f)) * 0.7f;
        float i = Mathf.Clamp01(Mathf.Sin(cycle * 3.3f + 1.2f)) * 0.5f;
        float u = Mathf.Clamp01(Mathf.Sin(cycle * 2.7f + 2.4f)) * 0.4f;
        float e = Mathf.Clamp01(Mathf.Sin(cycle * 3.8f + 3.6f)) * 0.5f;
        float o = Mathf.Clamp01(Mathf.Sin(cycle * 2.5f + 4.8f)) * 0.6f;

        // Occasional pauses
        float pauseGate = Mathf.PerlinNoise(t * 2f, 0) > 0.35f ? 1f : 0.1f;
        a *= pauseGate;
        i *= pauseGate;
        u *= pauseGate;
        e *= pauseGate;
        o *= pauseGate;

        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.A, a);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.I, i);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.U, u);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.E, e);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.O, o);
    }

    void ResetMouth()
    {
        if (blendShapeProxy == null) return;
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.A, 0);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.I, 0);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.U, 0);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.E, 0);
        blendShapeProxy.ImmediatelySetValue(BlendShapePreset.O, 0);
    }

    /// <summary>
    /// Sets a facial expression via BlendShape (Joy, Angry, Sorrow).
    /// Resets after the specified duration.
    /// </summary>
    public void SetExpression(BlendShapePreset preset, float value, float duration)
    {
        StartCoroutine(ExpressionRoutine(preset, value, duration));
    }

    IEnumerator ExpressionRoutine(BlendShapePreset preset, float value, float duration)
    {
        if (blendShapeProxy == null) yield break;
        blendShapeProxy.ImmediatelySetValue(preset, value);
        yield return new WaitForSeconds(duration);
        blendShapeProxy.ImmediatelySetValue(preset, 0);
    }

    AudioClip GenerateProceduralSpeechClip(float duration)
    {
        int sampleRate = 22050;
        int sampleCount = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];

        for (int s = 0; s < sampleCount; s++)
        {
            float t = (float)s / sampleRate;
            float freqMod = Mathf.Sin(t * 3.5f) * 30f + Mathf.Sin(t * 7f) * 15f;
            float freq = baseFrequency + freqMod;
            float formant1 = Mathf.Sin(2 * Mathf.PI * freq * t);
            float formant2 = Mathf.Sin(2 * Mathf.PI * freq * 2.3f * t) * 0.3f;
            float noise = (Random.value - 0.5f) * 0.15f;
            float envelope = Mathf.Clamp01(t * 10f) * Mathf.Clamp01((duration - t) * 10f);
            float pauseGate = Mathf.PerlinNoise(t * 2f, 0.5f) > 0.3f ? 1f : 0.05f;
            samples[s] = (formant1 + formant2 + noise) * volume * envelope * pauseGate;
        }

        AudioClip clip = AudioClip.Create("FallbackSpeech", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
