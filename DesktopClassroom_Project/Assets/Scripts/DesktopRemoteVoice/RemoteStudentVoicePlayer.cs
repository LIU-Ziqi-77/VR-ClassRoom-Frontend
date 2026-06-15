using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VRM;

[DisallowMultipleComponent]
public class RemoteStudentVoicePlayer : MonoBehaviour
{
    private static readonly BlendShapeKey AKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.A);
    private static readonly BlendShapeKey IKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.I);
    private static readonly BlendShapeKey UKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.U);
    private static readonly BlendShapeKey EKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.E);
    private static readonly BlendShapeKey OKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.O);

    [Header("Identity")]
    public int studentIndex;
    public string studentId;
    public string studentDisplayName;

    [Header("References")]
    public StudentBehaviorController studentController;
    public ProceduralBehaviorAnimator proceduralAnimator;
    public FallbackSpeechService fallbackSpeechService;
    public VRMBlendShapeProxy blendShapeProxy;
    public AudioSource audioSource;

    [Header("Streaming")]
    [Range(0.1f, 4f)] public float playbackGain = 1.4f;
    [Range(0.2f, 5f)] public float bufferSeconds = 2f;
    [Range(0.2f, 3f)] public float autoStopAfterSilence = 0.8f;
    [Range(0f, 1f)] public float mouthScale = 0.85f;
    [Range(0.5f, 20f)] public float mouthResponsiveness = 12f;
    [Range(0.5f, 5f)] public float speakingMotionRefreshSeconds = 1.1f;

    private readonly Queue<float> sampleQueue = new Queue<float>(48000);
    private readonly object sampleLock = new object();
    private AudioClip streamingClip;
    private int activeSampleRate = 16000;
    private int maxQueuedSamples = 32000;
    private bool isStreaming;
    private float lastPacketRealtime;
    private float targetMouth;
    private float currentMouth;
    private float nextSpeakingMotionTime;
    private int droppedSamples;
    private Coroutine presetClipRoutine;

    public bool IsStreaming => isStreaming;
    public int QueuedSampleCount
    {
        get
        {
            lock (sampleLock)
            {
                return sampleQueue.Count;
            }
        }
    }

    void Awake()
    {
        ResolveReferences();
    }

    void Update()
    {
        if (!isStreaming) return;

        if (Time.realtimeSinceStartup - lastPacketRealtime > autoStopAfterSilence)
        {
            EndStream("timeout");
            return;
        }

        RefreshSpeakingMotion();
        UpdateMouth();
    }

    void OnDisable()
    {
        EndStream("disabled");
        StopPresetClipPlayback();
    }

    public void ConfigureIdentity(int index, string id, string displayName)
    {
        studentIndex = index;
        studentId = id;
        studentDisplayName = displayName;
    }

    public void BeginStream(int sampleRate)
    {
        ResolveReferences();
        StopPresetClipPlayback();

        activeSampleRate = Mathf.Clamp(sampleRate > 0 ? sampleRate : 16000, 8000, 48000);
        maxQueuedSamples = Mathf.Max(activeSampleRate, Mathf.RoundToInt(activeSampleRate * bufferSeconds));

        lock (sampleLock)
        {
            sampleQueue.Clear();
            droppedSamples = 0;
        }

        EnsureAudioSource();
        EnsureStreamingClip();

        lastPacketRealtime = Time.realtimeSinceStartup;
        targetMouth = 0f;
        currentMouth = 0f;
        isStreaming = true;

        if (fallbackSpeechService != null && fallbackSpeechService.isSpeaking)
        {
            fallbackSpeechService.StopSpeaking();
        }

        if (studentController != null)
        {
            studentController.BeginExternalSpeaking();
        }
        else
        {
            RefreshSpeakingMotion(true);
        }

        audioSource.clip = streamingClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = 1f;

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        Debug.Log($"[RemoteVoice] {DisplayLabel()} stream started at {activeSampleRate} Hz.");
    }

    public void AppendPcm16(byte[] pcmBytes, int channels, float packetGain)
    {
        if (pcmBytes == null || pcmBytes.Length < 2) return;
        if (!isStreaming)
        {
            BeginStream(activeSampleRate);
        }

        int sourceChannels = Mathf.Max(1, channels);
        int frameSizeBytes = sourceChannels * 2;
        int frameCount = pcmBytes.Length / frameSizeBytes;
        if (frameCount <= 0) return;

        float gain = Mathf.Max(0.01f, packetGain <= 0f ? 1f : packetGain);
        float sumSq = 0f;
        int actualSamples = 0;

        lock (sampleLock)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                float mixed = 0f;
                for (int channel = 0; channel < sourceChannels; channel++)
                {
                    int offset = (frame * sourceChannels + channel) * 2;
                    short raw = BitConverter.ToInt16(pcmBytes, offset);
                    mixed += raw / 32768f;
                }

                mixed = Mathf.Clamp(mixed / sourceChannels * gain, -1f, 1f);
                sampleQueue.Enqueue(mixed);
                sumSq += mixed * mixed;
                actualSamples++;

                while (sampleQueue.Count > maxQueuedSamples)
                {
                    sampleQueue.Dequeue();
                    droppedSamples++;
                }
            }
        }

        if (actualSamples > 0)
        {
            float rms = Mathf.Sqrt(sumSq / actualSamples);
            targetMouth = Mathf.Clamp01(rms * 7.5f * mouthScale);
        }

        lastPacketRealtime = Time.realtimeSinceStartup;
    }

    public void EndStream(string reason = "remote stop")
    {
        if (!isStreaming) return;

        isStreaming = false;

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        lock (sampleLock)
        {
            sampleQueue.Clear();
        }

        ResetMouth();

        if (studentController != null)
        {
            studentController.EndExternalSpeaking();
        }

        Debug.Log($"[RemoteVoice] {DisplayLabel()} stream ended ({reason}). Dropped samples={droppedSamples}.");
    }

    public void PlayPresetClip(AudioClip clip, string label = "")
    {
        if (clip == null)
        {
            Debug.LogWarning($"[RemoteVoice] {DisplayLabel()} preset clip is null.");
            return;
        }

        ResolveReferences();
        EndStream("preset clip");
        StopPresetClipPlayback();

        if (studentController != null)
        {
            _ = PlayPresetThroughStudentController(clip, label);
            return;
        }

        presetClipRoutine = StartCoroutine(PresetClipRoutine(clip, label));
    }

    private async Task PlayPresetThroughStudentController(AudioClip clip, string label)
    {
        try
        {
            await studentController.SpeakAudioClipWithLipSync(clip, label);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RemoteVoice] StudentController preset playback failed for {DisplayLabel()}: {e.Message}");
        }
    }

    private IEnumerator PresetClipRoutine(AudioClip clip, string label)
    {
        EnsureAudioSource();
        if (fallbackSpeechService != null && fallbackSpeechService.isSpeaking)
        {
            fallbackSpeechService.StopSpeaking();
        }

        isStreaming = false;
        currentMouth = 0f;
        targetMouth = 0f;

        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.volume = 1f;
        audioSource.Play();

        RefreshSpeakingMotion(true);
        nextSpeakingMotionTime = Time.time + speakingMotionRefreshSeconds;

        Debug.Log($"[RemoteVoice] {DisplayLabel()} preset line: {label} ({clip.length:F2}s).");

        while (audioSource != null && audioSource.isPlaying)
        {
            if (proceduralAnimator != null && Time.time >= nextSpeakingMotionTime)
            {
                RefreshSpeakingMotion(true);
                nextSpeakingMotionTime = Time.time + speakingMotionRefreshSeconds;
            }

            targetMouth = EstimateClipMouth(clip, audioSource.time);
            UpdateMouth();
            yield return null;
        }

        ResetMouth();
        presetClipRoutine = null;
    }

    private void StopPresetClipPlayback()
    {
        if (presetClipRoutine != null)
        {
            StopCoroutine(presetClipRoutine);
            presetClipRoutine = null;
        }

        if (audioSource != null && audioSource.isPlaying && !isStreaming)
        {
            audioSource.Stop();
        }

        ResetMouth();
    }

    public void TriggerBehavior(string behavior, float duration)
    {
        ResolveReferences();
        string key = NormalizeBehavior(behavior);
        float safeDuration = duration > 0f ? duration : 3f;

        if (key == "stop")
        {
            EndStream("behavior stop");
            if (studentController != null) studentController.StopCurrentBehavior();
            if (proceduralAnimator != null) proceduralAnimator.StopCurrentBehavior();
            return;
        }

        if (proceduralAnimator != null && TryTriggerProceduralBehavior(key, safeDuration))
        {
            return;
        }

        if (studentController != null && TryTriggerControllerBehavior(key, safeDuration))
        {
            return;
        }

        Debug.LogWarning($"[RemoteVoice] Unsupported behavior '{behavior}' for {DisplayLabel()}.");
    }

    private bool TryTriggerProceduralBehavior(string key, float duration)
    {
        switch (key)
        {
            case "raisehand":
            case "raisinghand":
                proceduralAnimator.PlayRaiseHand(duration);
                return true;
            case "askquestion":
                proceduralAnimator.PlayAskQuestion(duration);
                return true;
            case "takenotes":
                proceduralAnimator.PlayTakeNotes(duration);
                return true;
            case "distracted":
            case "offtask":
                proceduralAnimator.PlayDistracted(duration);
                return true;
            case "talk":
            case "talktoclassmate":
            case "selftalk":
                proceduralAnimator.PlayTalkToClassmate(FindNearestPeer(), duration);
                return true;
            case "scream":
                proceduralAnimator.PlayScream(duration);
                return true;
            case "hitdesk":
                proceduralAnimator.PlayHitDesk(duration);
                return true;
            case "liedown":
            case "slump":
                proceduralAnimator.PlayLieDown(duration);
                return true;
            case "recover":
            case "return":
                proceduralAnimator.PlayRecoverFromLieDown();
                return true;
            case "leaveseat":
                Vector3 target = transform.position + transform.right * 1.2f + transform.forward * 0.35f;
                proceduralAnimator.PlayLeaveSeat(target, Mathf.Clamp(duration, 1f, 4f));
                return true;
        }

        return false;
    }

    private bool TryTriggerControllerBehavior(string key, float duration)
    {
        StudentBehaviorType behavior;
        switch (key)
        {
            case "raisehand":
            case "raisinghand":
                behavior = StudentBehaviorType.RaisingHand;
                break;
            case "confused":
                behavior = StudentBehaviorType.Confused;
                break;
            case "excited":
                behavior = StudentBehaviorType.Excited;
                break;
            case "distracted":
            case "offtask":
                behavior = StudentBehaviorType.OffTask;
                break;
            case "lookatboard":
                behavior = StudentBehaviorType.LookAtBoard;
                break;
            case "leaveseat":
                behavior = StudentBehaviorType.LeaveSeat;
                break;
            case "selftalk":
            case "talk":
                behavior = StudentBehaviorType.SelfTalk;
                break;
            case "scream":
                behavior = StudentBehaviorType.Scream;
                break;
            case "hitdesk":
                behavior = StudentBehaviorType.HitDesk;
                break;
            case "liedown":
            case "slump":
                behavior = StudentBehaviorType.LieDown;
                break;
            default:
                return false;
        }

        studentController.SetBehavior(behavior, duration);
        return true;
    }

    private void ResolveReferences()
    {
        if (studentController == null) studentController = GetComponent<StudentBehaviorController>();
        if (proceduralAnimator == null) proceduralAnimator = GetComponent<ProceduralBehaviorAnimator>();
        if (fallbackSpeechService == null) fallbackSpeechService = GetComponent<FallbackSpeechService>();
        if (blendShapeProxy == null)
        {
            blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
            if (blendShapeProxy == null) blendShapeProxy = GetComponentInChildren<VRMBlendShapeProxy>();
            if (blendShapeProxy == null) blendShapeProxy = GetComponentInParent<VRMBlendShapeProxy>();
        }
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void EnsureStreamingClip()
    {
        if (streamingClip != null && streamingClip.frequency == activeSampleRate) return;

        int clipSamples = Mathf.Max(activeSampleRate, Mathf.RoundToInt(activeSampleRate * bufferSeconds));
        streamingClip = AudioClip.Create(
            $"RemoteVoice_{DisplayLabel()}",
            clipSamples,
            1,
            activeSampleRate,
            true,
            OnAudioRead,
            OnAudioSetPosition);
    }

    private void OnAudioRead(float[] data)
    {
        lock (sampleLock)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = sampleQueue.Count > 0
                    ? Mathf.Clamp(sampleQueue.Dequeue() * playbackGain, -1f, 1f)
                    : 0f;
            }
        }
    }

    private void OnAudioSetPosition(int position)
    {
    }

    private void RefreshSpeakingMotion(bool force = false)
    {
        if (studentController != null || proceduralAnimator == null) return;
        if (!force && Time.time < nextSpeakingMotionTime) return;

        proceduralAnimator.PlaySpeakingMotion(speakingMotionRefreshSeconds + 0.2f);
        nextSpeakingMotionTime = Time.time + speakingMotionRefreshSeconds;
    }

    private float EstimateClipMouth(AudioClip clip, float timeSeconds)
    {
        if (clip == null) return 0f;

        int windowSamples = Mathf.Clamp(Mathf.RoundToInt(clip.frequency * 0.035f), 128, 2048);
        int centerSample = Mathf.Clamp(Mathf.RoundToInt(timeSeconds * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
        int startSample = Mathf.Clamp(centerSample - windowSamples / 2, 0, Mathf.Max(0, clip.samples - windowSamples));
        int totalFloats = windowSamples * clip.channels;
        float[] buffer = new float[totalFloats];

        if (!clip.GetData(buffer, startSample)) return 0f;

        float sumSq = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            sumSq += buffer[i] * buffer[i];
        }

        float rms = Mathf.Sqrt(sumSq / Mathf.Max(1, buffer.Length));
        return Mathf.Clamp01(rms * 8f * mouthScale);
    }

    private void UpdateMouth()
    {
        currentMouth = Mathf.Lerp(currentMouth, targetMouth, Time.deltaTime * mouthResponsiveness);
        targetMouth = Mathf.MoveTowards(targetMouth, 0f, Time.deltaTime * 1.5f);

        if (blendShapeProxy == null) return;

        float a = currentMouth;
        float o = Mathf.Clamp01(currentMouth * 0.55f + Mathf.PerlinNoise(Time.time * 6f, studentIndex) * 0.08f);
        float i = Mathf.Clamp01(currentMouth * 0.25f);

        blendShapeProxy.ImmediatelySetValue(AKey, a);
        blendShapeProxy.ImmediatelySetValue(OKey, o);
        blendShapeProxy.ImmediatelySetValue(IKey, i);
        blendShapeProxy.ImmediatelySetValue(UKey, 0f);
        blendShapeProxy.ImmediatelySetValue(EKey, 0f);
    }

    private void ResetMouth()
    {
        currentMouth = 0f;
        targetMouth = 0f;
        if (blendShapeProxy == null) return;

        blendShapeProxy.ImmediatelySetValue(AKey, 0f);
        blendShapeProxy.ImmediatelySetValue(IKey, 0f);
        blendShapeProxy.ImmediatelySetValue(UKey, 0f);
        blendShapeProxy.ImmediatelySetValue(EKey, 0f);
        blendShapeProxy.ImmediatelySetValue(OKey, 0f);
    }

    private Transform FindNearestPeer()
    {
        RemoteStudentVoicePlayer[] peers = FindObjectsByType<RemoteStudentVoicePlayer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        Transform nearest = null;
        float best = float.MaxValue;

        foreach (RemoteStudentVoicePlayer peer in peers)
        {
            if (peer == null || peer == this) continue;
            float sqr = (peer.transform.position - transform.position).sqrMagnitude;
            if (sqr < best)
            {
                best = sqr;
                nearest = peer.transform;
            }
        }

        return nearest;
    }

    private static string NormalizeBehavior(string behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior)) return "";
        return behavior.Trim().ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private string DisplayLabel()
    {
        if (!string.IsNullOrWhiteSpace(studentDisplayName)) return studentDisplayName;
        if (!string.IsNullOrWhiteSpace(studentId)) return studentId;
        return gameObject.name;
    }
}
