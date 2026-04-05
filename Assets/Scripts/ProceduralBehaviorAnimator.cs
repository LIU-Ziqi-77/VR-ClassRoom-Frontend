using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Procedural bone-based animation system for student avatar behaviors.
/// Uses Unity Humanoid bone transforms via Animator.GetBoneTransform().
/// Bone overrides are applied in LateUpdate() to work on top of Animator.
///
/// TEMPORARY FRONTEND PLACEHOLDER — replace individual behaviors
/// with proper animation clips when available.
/// </summary>
public class ProceduralBehaviorAnimator : MonoBehaviour
{
    [Header("References")]
    public Animator animator;

    [Header("Tuning")]
    public float defaultTransitionTime = 0.4f;

    private Coroutine _activeBehavior;
    private bool _behaviorActive;

    private Dictionary<HumanBodyBones, Quaternion> _restPose = new Dictionary<HumanBodyBones, Quaternion>();
    private Dictionary<HumanBodyBones, Quaternion> _overrides = new Dictionary<HumanBodyBones, Quaternion>();
    private Dictionary<HumanBodyBones, float> _overrideWeights = new Dictionary<HumanBodyBones, float>();
    private bool _restPoseCaptured;
    private bool _applyOverrides;

    static readonly HumanBodyBones[] TrackedBones = {
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
        HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
    };

    void Awake()
    {
        if (animator == null)
            animator = FindHumanoidAnimator();

        if (animator == null)
        {
            Debug.LogError($"[PBA] {gameObject.name}: No Animator found at all! Bone control disabled.");
            return;
        }

        if (!animator.isHuman)
        {
            Debug.LogWarning($"[PBA] {gameObject.name}: Animator '{animator.name}' is NOT humanoid (avatar={animator.avatar}). " +
                             "GetBoneTransform() will return null. Check Avatar import settings.");
        }
        else
        {
            var testBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Debug.Log($"[PBA] {gameObject.name}: Humanoid Animator OK. Head bone = {(testBone != null ? testBone.name : "NULL")}");
        }
    }

    Animator FindHumanoidAnimator()
    {
        var animators = GetComponents<Animator>();
        foreach (var a in animators)
            if (a.isHuman) return a;
        return animators.Length > 0 ? animators[0] : null;
    }

    void LateUpdate()
    {
        if (animator == null || !animator.isHuman) return;

        if (!_restPoseCaptured)
        {
            CaptureRestPose();
            return;
        }

        if (!_applyOverrides) return;

        foreach (var kv in _overrides)
        {
            Transform bone = animator.GetBoneTransform(kv.Key);
            if (bone == null) continue;

            float weight = _overrideWeights.ContainsKey(kv.Key) ? _overrideWeights[kv.Key] : 1f;
            if (weight <= 0.001f) continue;

            Quaternion restRot = _restPose.ContainsKey(kv.Key) ? _restPose[kv.Key] : bone.localRotation;
            bone.localRotation = Quaternion.Slerp(restRot, kv.Value, weight);
        }
    }

    void CaptureRestPose()
    {
        int found = 0;
        foreach (var bone in TrackedBones)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null)
            {
                _restPose[bone] = t.localRotation;
                found++;
            }
        }
        _restPoseCaptured = true;
        Debug.Log($"[PBA] {gameObject.name}: Rest pose captured — {found}/{TrackedBones.Length} bones mapped");
    }

    Quaternion Rest(HumanBodyBones bone)
    {
        return _restPose.ContainsKey(bone) ? _restPose[bone] : Quaternion.identity;
    }

    void SetOverride(HumanBodyBones bone, Quaternion rotation, float weight = 1f)
    {
        _overrides[bone] = rotation;
        _overrideWeights[bone] = weight;
        _applyOverrides = true;
    }

    void ClearOverrides()
    {
        _overrides.Clear();
        _overrideWeights.Clear();
        _applyOverrides = false;
    }

    // ─── Public API ──────────────────────────────────────────

    public void StopCurrentBehavior()
    {
        if (_activeBehavior != null)
        {
            StopCoroutine(_activeBehavior);
            _activeBehavior = null;
        }
        _behaviorActive = false;
        StartCoroutine(FadeOutOverrides(0.4f));
    }

    public bool IsBehaviorActive => _behaviorActive;

    public void PlayRaiseHand(float duration = 0f)
    {
        StartBehavior(RaiseHandRoutine(duration));
    }

    public void PlayTakeNotes(float duration = 0f)
    {
        StartBehavior(TakeNotesRoutine(duration));
    }

    public void PlayScream(float duration = 2f)
    {
        StartBehavior(ScreamRoutine(duration));
    }

    public void PlayHitDesk(float duration = 3f)
    {
        StartBehavior(HitDeskRoutine(duration));
    }

    public void PlayPushClassmate(Transform target, float duration = 2f)
    {
        StartBehavior(PushClassmateRoutine(target, duration));
    }

    public void PlayLieDown(float duration = 0f)
    {
        StartBehavior(LieDownRoutine(duration));
    }

    public void PlaySpeakingMotion(float duration)
    {
        StartBehavior(SpeakingMotionRoutine(duration));
    }

    public void PlayReaction(string reactionType, float duration = 1.5f)
    {
        StartBehavior(ReactionRoutine(reactionType, duration));
    }

    // ─── Internal ────────────────────────────────────────────

    void StartBehavior(IEnumerator routine)
    {
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"[PBA] {gameObject.name}: StartBehavior skipped — no humanoid Animator");
            return;
        }
        if (_activeBehavior != null)
            StopCoroutine(_activeBehavior);
        ClearOverrides();
        _behaviorActive = true;
        _activeBehavior = StartCoroutine(WrapBehavior(routine));
        Debug.Log($"[PBA] {gameObject.name}: Behavior started (restPose captured={_restPoseCaptured})");
    }

    IEnumerator WrapBehavior(IEnumerator inner)
    {
        yield return StartCoroutine(inner);
        _behaviorActive = false;
        _activeBehavior = null;
        yield return StartCoroutine(FadeOutOverrides(0.3f));
    }

    IEnumerator FadeOutOverrides(float seconds)
    {
        if (_overrides.Count == 0)
        {
            _applyOverrides = false;
            yield break;
        }

        var snapshot = new Dictionary<HumanBodyBones, Quaternion>(_overrides);
        float elapsed = 0;
        while (elapsed < seconds)
        {
            float t = 1f - (elapsed / seconds);
            foreach (var kv in snapshot)
                _overrideWeights[kv.Key] = t;
            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearOverrides();
    }

    /// Smoothly transitions a set of bones from rest to target over `seconds`
    IEnumerator TransitionTo(Dictionary<HumanBodyBones, Quaternion> targets, float seconds)
    {
        float elapsed = 0;
        while (elapsed < seconds)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / seconds);
            foreach (var kv in targets)
                SetOverride(kv.Key, kv.Value, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        foreach (var kv in targets)
            SetOverride(kv.Key, kv.Value, 1f);
    }

    // ─── World-Space Aiming Utility ─────────────────────────

    /// <summary>
    /// Computes the localRotation that redirects a bone so it points toward
    /// worldTargetDir instead of its current direction.  Rig-agnostic: works
    /// by measuring actual bone→child vector and applying FromToRotation.
    /// </summary>
    Quaternion AimBone(HumanBodyBones bone, HumanBodyBones childBone, Vector3 worldTargetDir)
    {
        Transform boneT = animator.GetBoneTransform(bone);
        Transform childT = animator.GetBoneTransform(childBone);
        if (boneT == null || childT == null) return Rest(bone);

        Vector3 currentDir = (childT.position - boneT.position).normalized;
        Quaternion worldDelta = Quaternion.FromToRotation(currentDir, worldTargetDir.normalized);
        Quaternion newWorldRot = worldDelta * boneT.rotation;
        Quaternion parentWorld = boneT.parent != null ? boneT.parent.rotation : Quaternion.identity;
        return Quaternion.Inverse(parentWorld) * newWorldRot;
    }

    // ─── Behavior Implementations ────────────────────────────

    /// RAISE HAND — rig-agnostic version using world-space bone aiming.
    /// The upper arm is aimed upward (+Y) with a slight rightward lean so
    /// the silhouette reads clearly as "classroom hand-raise."
    IEnumerator RaiseHandRoutine(float duration)
    {
        while (!_restPoseCaptured) yield return null;

        Vector3 up = Vector3.up;
        Vector3 right = transform.right;
        Vector3 fwd = transform.forward;

        // Upper arm: mostly up, slightly to the avatar's right + a hint forward
        Vector3 upperArmDir = (up * 4f + right * 0.6f + fwd * 0.4f).normalized;
        Quaternion upperArmTarget = AimBone(
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, upperArmDir);

        // Lower arm (forearm): continue upward, slightly back (natural elbow bend)
        Vector3 forearmDir = (up * 3f - fwd * 0.8f + right * 0.2f).normalized;
        Quaternion lowerArmTarget = AimBone(
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, forearmDir);

        // Head: slight upward tilt (looking at own hand)
        Quaternion headTarget = Rest(HumanBodyBones.Head) * Quaternion.Euler(-5f, 3f, 0);

        var pose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.RightUpperArm] = upperArmTarget,
            [HumanBodyBones.RightLowerArm] = lowerArmTarget,
            [HumanBodyBones.Head] = headTarget,
        };

        yield return StartCoroutine(TransitionTo(pose, 0.5f));

        // Gentle hand sway while holding
        float t = 0;
        float endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            float sway = Mathf.Sin(t * 1.8f) * 2f;
            SetOverride(HumanBodyBones.RightHand,
                Rest(HumanBodyBones.RightHand) * Quaternion.Euler(sway, 0, sway * 0.5f));
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// TAKE NOTES: head tilt down, writing motion with right hand
    IEnumerator TakeNotesRoutine(float duration)
    {
        var pose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(25f, 0, 0),
            [HumanBodyBones.Neck] = Rest(HumanBodyBones.Neck) * Quaternion.Euler(10f, 0, 0),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(40f, 0, -30f),
            [HumanBodyBones.RightLowerArm] = Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, -60f, 0),
        };

        yield return StartCoroutine(TransitionTo(pose, 0.5f));

        float t = 0;
        float endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            float writeX = Mathf.Sin(t * 6f) * 8f;
            float writeY = Mathf.Sin(t * 6f) * 4f;
            SetOverride(HumanBodyBones.RightHand,
                Rest(HumanBodyBones.RightHand) * Quaternion.Euler(writeX, writeY, 0));
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// SCREAM: head thrown back, arms spread, body shaking
    IEnumerator ScreamRoutine(float duration)
    {
        var pose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(-20f, 0, 0),
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(-5f, 0, 0),
            [HumanBodyBones.LeftUpperArm] = Rest(HumanBodyBones.LeftUpperArm) * Quaternion.Euler(0, 0, 30f),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(0, 0, -30f),
        };

        yield return StartCoroutine(TransitionTo(pose, 0.2f));

        float t = 0;
        while (t < duration && _behaviorActive)
        {
            float shake = Mathf.Sin(t * 25f) * 3f;
            SetOverride(HumanBodyBones.Head, Rest(HumanBodyBones.Head) * Quaternion.Euler(-20f + shake, shake * 0.5f, 0));
            SetOverride(HumanBodyBones.Spine, Rest(HumanBodyBones.Spine) * Quaternion.Euler(-5f, shake * 0.3f, 0));
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// HIT DESK: repeated right arm slam toward desk
    IEnumerator HitDeskRoutine(float duration)
    {
        float t = 0;
        int hits = 0;
        int maxHits = Mathf.Max(2, Mathf.RoundToInt(duration / 0.6f));

        while (t < duration && hits < maxHits && _behaviorActive)
        {
            // Raise arm phase
            float phase = 0;
            while (phase < 0.2f && _behaviorActive)
            {
                float p = phase / 0.2f;
                SetOverride(HumanBodyBones.RightUpperArm,
                    Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(20f * p, 0, -40f * p));
                SetOverride(HumanBodyBones.RightLowerArm,
                    Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, 0, 0));
                phase += Time.deltaTime;
                t += Time.deltaTime;
                yield return null;
            }

            // Slam down phase
            phase = 0;
            while (phase < 0.15f && _behaviorActive)
            {
                float p = phase / 0.15f;
                SetOverride(HumanBodyBones.RightUpperArm,
                    Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(50f * p, 0, 0));
                SetOverride(HumanBodyBones.RightLowerArm,
                    Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, -80f * p, 0));
                SetOverride(HumanBodyBones.Spine,
                    Rest(HumanBodyBones.Spine) * Quaternion.Euler(5f * p, 0, 0));
                phase += Time.deltaTime;
                t += Time.deltaTime;
                yield return null;
            }

            // Impact hold
            yield return new WaitForSeconds(0.15f);
            t += 0.15f;
            hits++;
        }
    }

    /// PUSH CLASSMATE: lean torso + extend arms toward target
    IEnumerator PushClassmateRoutine(Transform target, float duration)
    {
        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.right;

        float yAngle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);
        float yaw = Mathf.Clamp(yAngle, -60f, 60f);

        var leanPose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(15f, yaw * 0.5f, 0),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(40f, yaw * 0.3f, -20f),
            [HumanBodyBones.RightLowerArm] = Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, -40f, 0),
            [HumanBodyBones.LeftUpperArm] = Rest(HumanBodyBones.LeftUpperArm) * Quaternion.Euler(40f, yaw * 0.3f, 20f),
            [HumanBodyBones.LeftLowerArm] = Rest(HumanBodyBones.LeftLowerArm) * Quaternion.Euler(0, 40f, 0),
        };

        yield return StartCoroutine(TransitionTo(leanPose, 0.3f));

        // Push thrust
        float thrust = 0;
        while (thrust < 0.3f && _behaviorActive)
        {
            float p = thrust / 0.3f;
            SetOverride(HumanBodyBones.Spine, Rest(HumanBodyBones.Spine) * Quaternion.Euler(25f * p, yaw * 0.7f, 0));
            thrust += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(Mathf.Max(0, duration - 0.6f));
    }

    /// LIE DOWN / SLUMP: spine and head collapse forward
    IEnumerator LieDownRoutine(float duration)
    {
        var pose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(35f, 0, 0),
            [HumanBodyBones.Neck] = Rest(HumanBodyBones.Neck) * Quaternion.Euler(25f, 0, 0),
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(30f, 15f, 0),
            [HumanBodyBones.LeftUpperArm] = Rest(HumanBodyBones.LeftUpperArm) * Quaternion.Euler(50f, 0, 20f),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(50f, 0, -20f),
            [HumanBodyBones.LeftLowerArm] = Rest(HumanBodyBones.LeftLowerArm) * Quaternion.Euler(0, 60f, 0),
            [HumanBodyBones.RightLowerArm] = Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, -60f, 0),
        };

        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            pose[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(20f, 0, 0);

        yield return StartCoroutine(TransitionTo(pose, 0.8f));

        float t = 0;
        float endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            float breath = Mathf.Sin(t * 1.5f) * 1.5f;
            SetOverride(HumanBodyBones.Spine, Rest(HumanBodyBones.Spine) * Quaternion.Euler(35f + breath, 0, 0));
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// SPEAKING MOTION: natural head movement with varied rhythm
    IEnumerator SpeakingMotionRoutine(float duration)
    {
        float t = 0;
        while (t < duration && _behaviorActive)
        {
            // Layered sine waves for organic-looking motion
            float nod = Mathf.Sin(t * 2.8f) * 3f + Mathf.Sin(t * 4.5f) * 1.5f;
            float tilt = Mathf.Sin(t * 1.4f) * 2.5f;
            float roll = Mathf.Sin(t * 1.9f) * 1f;

            // Occasional emphasis nods (simulates speech stress)
            float emphasis = Mathf.Clamp01(Mathf.Sin(t * 1.1f)) * Mathf.Sin(t * 6f) * 2f;
            nod += emphasis;

            SetOverride(HumanBodyBones.Head,
                Rest(HumanBodyBones.Head) * Quaternion.Euler(nod, tilt, roll));
            SetOverride(HumanBodyBones.Neck,
                Rest(HumanBodyBones.Neck) * Quaternion.Euler(nod * 0.3f, 0, 0));
            // Subtle spine sway while speaking
            SetOverride(HumanBodyBones.Spine,
                Rest(HumanBodyBones.Spine) * Quaternion.Euler(Mathf.Sin(t * 0.8f) * 1f, 0, 0));
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// REACTION: flinch when pushed by neighbor
    IEnumerator ReactionRoutine(string type, float duration)
    {
        var pose = new Dictionary<HumanBodyBones, Quaternion>();

        if (type == "pushed")
        {
            pose[HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(0, 0, -15f);
            pose[HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(0, 0, -10f);
            pose[HumanBodyBones.LeftUpperArm] = Rest(HumanBodyBones.LeftUpperArm) * Quaternion.Euler(0, 0, 30f);
        }
        else
        {
            pose[HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(-10f, 0, 0);
        }

        yield return StartCoroutine(TransitionTo(pose, 0.15f));
        yield return new WaitForSeconds(duration);
    }
}
