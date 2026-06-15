using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

    [Header("Touch Nose Pose")]
    [Tooltip("Approximate wrist offset from the face when touching the nose. X=student right, Y=up, Z=face forward.")]
    public Vector3 touchNoseWristOffset = new Vector3(0.045f, -0.055f, 0.075f);
    [Tooltip("Soft contact point used for the head to subtly meet the hand. X=student right, Y=up, Z=face forward.")]
    public Vector3 touchNoseFaceContactOffset = new Vector3(0.01f, -0.012f, 0.085f);

    private Coroutine _activeBehavior;
    private bool _behaviorActive;
    private bool _lieDownExitRequested;
    private PlayableGraph _clipGraph;
    private bool _clipChangedRootMotion;
    private bool _savedApplyRootMotion;

    /// <summary>Human-readable name of the currently running behavior ("" when idle).</summary>
    public string CurrentBehaviorName { get; private set; } = "";

    /// <summary>Seat / home pose captured on first LeaveSeat call; used by ReturnToSeat.</summary>
    [HideInInspector] public Vector3 seatPosition;
    [HideInInspector] public Quaternion seatRotation;
    [HideInInspector] public bool seatPositionCaptured;
    private Vector3 _leaveSeatStandingPosition;
    private bool _leaveSeatRootLowered;

    private Dictionary<HumanBodyBones, Quaternion> _restPose = new Dictionary<HumanBodyBones, Quaternion>();
    private Dictionary<HumanBodyBones, Quaternion> _overrides = new Dictionary<HumanBodyBones, Quaternion>();
    private Dictionary<HumanBodyBones, float> _overrideWeights = new Dictionary<HumanBodyBones, float>();
    private bool _restPoseCaptured;
    private bool _applyOverrides;

    private struct DeskSlumpTargets
    {
        public bool foundDesk;
        public Vector3 leftHand;
        public Vector3 rightHand;
        public Vector3 forward;
        public Vector3 right;
        public float surfaceY;
    }

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

    void OnDisable()
    {
        StopAnimationClipGraph();
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
        _lieDownExitRequested = true;
        if (_activeBehavior != null)
        {
            StopCoroutine(_activeBehavior);
            _activeBehavior = null;
        }
        StopAnimationClipGraph();
        _behaviorActive = false;
        CurrentBehaviorName = "";
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

    public void PlayLieDownHold()
    {
        StartBehavior(LieDownRoutine(0f));
    }

    public void PlayRecoverFromLieDown()
    {
        if (_behaviorActive && CurrentBehaviorName == "趴桌")
        {
            _lieDownExitRequested = true;
        }
    }

    public void PlayTouchNose(float duration = 3f)
    {
        StartBehavior(TouchNoseRoutine(duration));
    }

    public void PlaySpeakingMotion(float duration)
    {
        StartBehavior(SpeakingMotionRoutine(duration));
    }

    public void PlayReaction(string reactionType, float duration = 1.5f)
    {
        StartBehavior(ReactionRoutine(reactionType, duration));
    }

    /// <summary>Ask question: raise hand with a forward lean and eager nodding.</summary>
    public void PlayAskQuestion(float duration = 0f)
    {
        StartBehavior(AskQuestionRoutine(duration));
    }

    /// <summary>Distracted: slouch, look away, idle fidgeting.</summary>
    public void PlayDistracted(float duration = 0f)
    {
        StartBehavior(DistractedRoutine(duration));
    }

    /// <summary>Talk to nearby classmate: turn toward neighbor with speaking motion.</summary>
    public void PlayTalkToClassmate(Transform neighbor, float duration = 0f)
    {
        StartBehavior(TalkToClassmateRoutine(neighbor, duration));
    }

    /// <summary>
    /// Leave seat: approximate stand-up pose and translate toward targetWorldPos.
    /// Captures seat pose on first call so ReturnToSeat can undo it.
    /// </summary>
    public void PlayLeaveSeat(
        Vector3 targetWorldPos,
        float moveDuration = 2.5f,
        AnimationClip layingHoldClip = null,
        float layingRootYOffset = 0f)
    {
        if (!seatPositionCaptured)
        {
            seatPosition = transform.position;
            seatRotation = transform.rotation;
            seatPositionCaptured = true;
        }
        StartBehavior(LeaveSeatRoutine(targetWorldPos, moveDuration, layingHoldClip, layingRootYOffset));
    }

    /// <summary>Return to the captured seat position.</summary>
    public void PlayReturnToSeat(float moveDuration = 2f, AnimationClip gettingUpClip = null)
    {
        if (!seatPositionCaptured) return;
        StartBehavior(ReturnToSeatRoutine(moveDuration, gettingUpClip));
    }

    /// <summary>Pushed reaction with directional displacement and stumble.</summary>
    public void PlayPushedReaction(Vector3 pushDirection, float duration = 1.5f)
    {
        StartBehavior(PushedReactionRoutine(pushDirection, duration));
    }

    /// <summary>Listen to a nearby classmate — face them with attentive nodding.</summary>
    public void PlayListenToClassmate(Transform speaker, float duration = 0f)
    {
        StartBehavior(ListenToClassmateRoutine(speaker, duration));
    }

    public void PlayAnimationClip(AnimationClip clip, float duration, string behaviorName)
    {
        PlayUpperBodyAnimationClip(clip, duration, behaviorName, 0.35f, 0.45f, 1f);
    }

    public void PlayUpperBodyAnimationClip(
        AnimationClip clip,
        float duration,
        string behaviorName,
        float blendInSeconds,
        float blendOutSeconds,
        float playbackSpeed)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[PBA] {gameObject.name}: PlayAnimationClip skipped — clip is null.");
            return;
        }

        StartClipBehavior(clip, duration, behaviorName, blendInSeconds, blendOutSeconds, playbackSpeed);
    }

    // ─── Internal ────────────────────────────────────────────

    void StartBehavior(IEnumerator routine, string behaviorName = "")
    {
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"[PBA] {gameObject.name}: StartBehavior skipped — no humanoid Animator");
            return;
        }
        if (_activeBehavior != null)
            StopCoroutine(_activeBehavior);
        StopAnimationClipGraph();
        ClearOverrides();
        _lieDownExitRequested = false;
        _behaviorActive = true;
        _activeBehavior = StartCoroutine(WrapBehavior(routine));
        Debug.Log($"[PBA] {gameObject.name}: Behavior started (restPose captured={_restPoseCaptured})");
    }

    void StartClipBehavior(
        AnimationClip clip,
        float duration,
        string behaviorName,
        float blendInSeconds,
        float blendOutSeconds,
        float playbackSpeed)
    {
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"[PBA] {gameObject.name}: PlayAnimationClip skipped — no humanoid Animator");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[PBA] {gameObject.name}: PlayAnimationClip skipped — no base AnimatorController to blend with.");
            return;
        }

        if (_activeBehavior != null)
            StopCoroutine(_activeBehavior);

        StopAnimationClipGraph();
        ClearOverrides();
        _behaviorActive = true;
        CurrentBehaviorName = behaviorName;
        _activeBehavior = StartCoroutine(AnimationClipRoutine(
            clip,
            Mathf.Max(0.1f, duration),
            behaviorName,
            Mathf.Max(0.01f, blendInSeconds),
            Mathf.Max(0.01f, blendOutSeconds),
            Mathf.Max(0.1f, playbackSpeed)));
    }

    IEnumerator WrapBehavior(IEnumerator inner)
    {
        yield return StartCoroutine(inner);
        _behaviorActive = false;
        _activeBehavior = null;
        CurrentBehaviorName = "";
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
        var startWeights = new Dictionary<HumanBodyBones, float>();
        foreach (var kv in snapshot)
        {
            startWeights[kv.Key] = _overrideWeights.ContainsKey(kv.Key) ? _overrideWeights[kv.Key] : 1f;
        }

        float elapsed = 0;
        while (elapsed < seconds)
        {
            float t = 1f - (elapsed / seconds);
            foreach (var kv in snapshot)
                _overrideWeights[kv.Key] = startWeights[kv.Key] * t;
            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearOverrides();
    }

    IEnumerator MoveRootToPosition(Vector3 targetPosition, float seconds)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        float safeSeconds = Mathf.Max(0.01f, seconds);

        while (elapsed < safeSeconds && _behaviorActive)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / safeSeconds);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
    }

    IEnumerator AnimationClipRoutine(
        AnimationClip clip,
        float duration,
        string behaviorName,
        float blendInSeconds,
        float blendOutSeconds,
        float playbackSpeed)
    {
        _savedApplyRootMotion = animator.applyRootMotion;
        _clipChangedRootMotion = true;
        animator.applyRootMotion = false;

        _clipGraph = PlayableGraph.Create($"{gameObject.name}_{behaviorName}");
        _clipGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimatorControllerPlayable basePlayable = AnimatorControllerPlayable.Create(_clipGraph, animator.runtimeAnimatorController);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_clipGraph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        clipPlayable.SetSpeed(playbackSpeed);

        AnimationLayerMixerPlayable layerMixer = AnimationLayerMixerPlayable.Create(_clipGraph, 2);
        layerMixer.SetLayerMaskFromAvatarMask(1, CreateUpperBodyAvatarMask());
        layerMixer.SetInputWeight(0, 1f);
        layerMixer.SetInputWeight(1, 0f);

        _clipGraph.Connect(basePlayable, 0, layerMixer, 0);
        _clipGraph.Connect(clipPlayable, 0, layerMixer, 1);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_clipGraph, "DTT Behavior Clip", animator);
        output.SetSourcePlayable(layerMixer);
        _clipGraph.Play();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float inWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendInSeconds));
            float outWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - elapsed) / blendOutSeconds));
            layerMixer.SetInputWeight(1, Mathf.Min(inWeight, outWeight));

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_clipGraph.IsValid())
        {
            layerMixer.SetInputWeight(1, 0f);
        }
        StopAnimationClipGraph();
        _behaviorActive = false;
        _activeBehavior = null;
        CurrentBehaviorName = "";
    }

    IEnumerator FullBodyClipInCurrentBehavior(
        AnimationClip clip,
        string behaviorName,
        float duration,
        float blendInSeconds,
        float playbackSpeed,
        bool loop)
    {
        if (clip == null || animator == null || !animator.isHuman)
            yield break;

        StopAnimationClipGraph();
        ClearOverrides();

        _savedApplyRootMotion = animator.applyRootMotion;
        _clipChangedRootMotion = true;
        animator.applyRootMotion = false;

        _clipGraph = PlayableGraph.Create($"{gameObject.name}_{behaviorName}_FullBody");
        _clipGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_clipGraph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        clipPlayable.SetSpeed(loop ? 0f : playbackSpeed);

        bool hasBaseController = animator.runtimeAnimatorController != null;
        AnimationMixerPlayable mixer = default;
        if (hasBaseController)
        {
            AnimatorControllerPlayable basePlayable = AnimatorControllerPlayable.Create(_clipGraph, animator.runtimeAnimatorController);
            mixer = AnimationMixerPlayable.Create(_clipGraph, 2);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            _clipGraph.Connect(basePlayable, 0, mixer, 0);
            _clipGraph.Connect(clipPlayable, 0, mixer, 1);
        }

        Playable sourcePlayable = hasBaseController ? (Playable)mixer : (Playable)clipPlayable;
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_clipGraph, "DTT Full Body Clip", animator);
        output.SetSourcePlayable(sourcePlayable);
        _clipGraph.Play();

        float elapsed = 0f;
        float clipLength = Mathf.Max(0.01f, clip.length);
        float safeBlendIn = Mathf.Max(0.01f, blendInSeconds);
        bool indefinite = duration < 0f;

        while (_behaviorActive && _clipGraph.IsValid() && (indefinite || elapsed < duration))
        {
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeBlendIn));
            if (hasBaseController)
            {
                if (!mixer.IsValid())
                    break;

                mixer.SetInputWeight(0, 1f - weight);
                mixer.SetInputWeight(1, weight);
            }

            if (loop)
            {
                if (!clipPlayable.IsValid())
                    break;

                clipPlayable.SetTime((elapsed * playbackSpeed) % clipLength);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopAnimationClipGraph();
    }

    AvatarMask CreateUpperBodyAvatarMask()
    {
        AvatarMask mask = new AvatarMask();
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
        return mask;
    }

    void StopAnimationClipGraph()
    {
        if (_clipGraph.IsValid())
        {
            _clipGraph.Destroy();
        }

        if (_clipChangedRootMotion && animator != null)
        {
            animator.applyRootMotion = _savedApplyRootMotion;
        }

        _clipChangedRootMotion = false;
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

    // ─── World-Space Aiming Utilities ────────────────────────

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

    /// <summary>
    /// Predictive AimBone: computes the local rotation for a child bone (e.g. forearm)
    /// assuming its parent bone (e.g. upper arm) WILL be at <paramref name="parentLocalTarget"/>
    /// rather than its current rotation. This avoids the two-phase compute-then-wait pattern
    /// that caused visual snapping.
    /// </summary>
    Quaternion AimBoneWithPredictedParent(HumanBodyBones bone, HumanBodyBones childBone,
        Vector3 worldTargetDir, Quaternion parentLocalTarget)
    {
        Transform boneT  = animator.GetBoneTransform(bone);
        Transform childT = animator.GetBoneTransform(childBone);
        Transform parentT = boneT != null ? boneT.parent : null;
        if (boneT == null || childT == null || parentT == null) return Rest(bone);

        Quaternion grandparentWorld = parentT.parent != null ? parentT.parent.rotation : Quaternion.identity;
        Quaternion parentFutureWorld = grandparentWorld * parentLocalTarget;
        Quaternion parentDelta = parentFutureWorld * Quaternion.Inverse(parentT.rotation);

        Vector3 pivot = parentT.position;
        Vector3 boneFuturePos  = pivot + parentDelta * (boneT.position  - pivot);
        Vector3 childFuturePos = pivot + parentDelta * (childT.position - pivot);
        Quaternion boneFutureRot = parentDelta * boneT.rotation;

        Vector3 currentDir = (childFuturePos - boneFuturePos).normalized;
        if (currentDir.sqrMagnitude < 0.001f) return Rest(bone);

        Quaternion worldDelta2 = Quaternion.FromToRotation(currentDir, worldTargetDir.normalized);
        Quaternion newWorldRot = worldDelta2 * boneFutureRot;
        return Quaternion.Inverse(parentFutureWorld) * newWorldRot;
    }

    // ─── Palm Correction Utility ─────────────────────────────

    /// <summary>
    /// Computes a hand local rotation that twists the wrist so the palm faces
    /// <paramref name="desiredPalmDirWorld"/> after the arm is raised.
    /// Uses runtime bone vectors for the twist axis (rig-agnostic) and the
    /// predicted forearm world rotation to convert the desired direction into
    /// the correct coordinate frame.
    /// </summary>
    Quaternion ComputePalmCorrectedHand(HumanBodyBones handBone,
        Vector3 desiredPalmDirWorld, Quaternion forearmFutureWorld)
    {
        Transform hand = animator.GetBoneTransform(handBone);
        if (hand == null || hand.parent == null || hand.localPosition.sqrMagnitude < 0.0001f)
            return Rest(handBone);

        // Twist axis = forearm bone direction in forearm's local space (constant)
        Vector3 twistAxisLocal = hand.localPosition.normalized;

        // Palm-facing direction in forearm local space from the rest hand rotation.
        // hand.up reads the Animator's bone orientation (rig-agnostic palm normal).
        Vector3 palmBackInForearmLocal = Quaternion.Inverse(hand.parent.rotation) * hand.up;
        Vector3 palmFacingLocal = -palmBackInForearmLocal;
        palmFacingLocal = Vector3.ProjectOnPlane(palmFacingLocal, twistAxisLocal).normalized;

        // Desired palm direction in the forearm's FUTURE local space
        Vector3 desiredLocal = Quaternion.Inverse(forearmFutureWorld) * desiredPalmDirWorld;
        desiredLocal = Vector3.ProjectOnPlane(desiredLocal, twistAxisLocal).normalized;

        if (palmFacingLocal.sqrMagnitude < 0.001f || desiredLocal.sqrMagnitude < 0.001f)
            return Rest(handBone);

        float angle = Vector3.SignedAngle(palmFacingLocal, desiredLocal, twistAxisLocal);
        return Quaternion.AngleAxis(angle, twistAxisLocal) * Rest(handBone);
    }

    /// Helper: predicts the forearm's world rotation after arm overrides are applied.
    Quaternion PredictForearmWorld(Quaternion upperArmLocalTarget, Quaternion lowerArmLocalTarget)
    {
        return PredictForearmWorld(HumanBodyBones.RightUpperArm, upperArmLocalTarget, lowerArmLocalTarget);
    }

    Quaternion PredictForearmWorld(HumanBodyBones upperArmBone, Quaternion upperArmLocalTarget, Quaternion lowerArmLocalTarget)
    {
        Transform ua = animator.GetBoneTransform(upperArmBone);
        if (ua == null) return Quaternion.identity;
        Quaternion shoulderWorld = ua.parent != null ? ua.parent.rotation : Quaternion.identity;
        return shoulderWorld * upperArmLocalTarget * lowerArmLocalTarget;
    }

    // ─── Face Targeting Utility ──────────────────────────────

    /// <summary>
    /// Computes yaw and pitch angles from this avatar's head to a target's head.
    /// Uses actual head bone positions for face-to-face accuracy rather than
    /// root transform positions which sit at floor/seat level.
    /// </summary>
    void ComputeFaceTargetAngles(Transform target, out float yaw, out float pitch)
    {
        yaw = 0f;
        pitch = 0f;
        if (target == null) return;

        Vector3 myHeadPos = transform.position + Vector3.up * 1.5f;
        Transform myHead = animator.GetBoneTransform(HumanBodyBones.Head);
        if (myHead != null) myHeadPos = myHead.position;

        Vector3 targetHeadPos = target.position + Vector3.up * 1.5f;
        Animator targetAnim = target.GetComponent<Animator>();
        if (targetAnim == null) targetAnim = target.GetComponentInChildren<Animator>();
        if (targetAnim != null && targetAnim.isHuman)
        {
            Transform targetHead = targetAnim.GetBoneTransform(HumanBodyBones.Head);
            if (targetHead != null) targetHeadPos = targetHead.position;
        }

        Vector3 toTarget = targetHeadPos - myHeadPos;
        Vector3 flatDir = toTarget;
        flatDir.y = 0;

        if (flatDir.sqrMagnitude > 0.001f)
            yaw = Vector3.SignedAngle(transform.forward, flatDir.normalized, Vector3.up);

        if (toTarget.sqrMagnitude > 0.001f && flatDir.magnitude > 0.01f)
            pitch = -Mathf.Atan2(toTarget.y, flatDir.magnitude) * Mathf.Rad2Deg;
    }

    // ─── Standing Pose Utilities ─────────────────────────────

    /// <summary>
    /// Computes local rotations for all four leg bones that produce straight
    /// standing legs. Uses AimBone to dynamically correct from whatever the
    /// Animator's current pose is (e.g. seated) — not reliant on rest pose
    /// which may have been captured from a sitting animation.
    /// Call after at least one frame with no overrides so bone positions are fresh.
    /// </summary>
    void ComputeStandingLegPose(
        out Quaternion leftUpper, out Quaternion rightUpper,
        out Quaternion leftLower, out Quaternion rightLower)
    {
        leftUpper  = AimBone(HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  Vector3.down);
        rightUpper = AimBone(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, Vector3.down);
        leftLower  = AimBoneWithPredictedParent(
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            Vector3.down, leftUpper);
        rightLower = AimBoneWithPredictedParent(
            HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            Vector3.down, rightUpper);
    }

    /// <summary>
    /// Computes local rotations that make both arms hang naturally at the sides,
    /// plus the bone-local swing axis for forward/backward arm swing during walking.
    /// Upper arms aimed straight down; forearms aimed slightly forward from
    /// vertical to give a relaxed elbow bend (~7°).
    /// </summary>
    void ComputeHangingArmPose(
        out Quaternion leftUpper, out Quaternion rightUpper,
        out Quaternion leftLower, out Quaternion rightLower,
        out Vector3 swingAxisL, out Vector3 swingAxisR)
    {
        leftUpper  = AimBone(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  Vector3.down);
        rightUpper = AimBone(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, Vector3.down);

        Transform lBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Quaternion lPW = (lBone != null && lBone.parent != null) ? lBone.parent.rotation : Quaternion.identity;
        Quaternion rPW = (rBone != null && rBone.parent != null) ? rBone.parent.rotation : Quaternion.identity;
        swingAxisL = (Quaternion.Inverse(lPW * leftUpper)  * transform.right).normalized;
        swingAxisR = (Quaternion.Inverse(rPW * rightUpper) * transform.right).normalized;

        Vector3 forearmDir = (Vector3.down * 8f + transform.forward * 1f).normalized;
        leftLower  = AimBoneWithPredictedParent(
            HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,
            forearmDir, leftUpper);
        rightLower = AimBoneWithPredictedParent(
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            forearmDir, rightUpper);
    }

    // ─── Behavior Implementations ────────────────────────────

    // ─── Behavior Name Tags (set at start of each routine) ───

    /// RAISE HAND — predictive AimBone for arm + rig-agnostic palm correction.
    IEnumerator RaiseHandRoutine(float duration)
    {
        CurrentBehaviorName = "举手";
        while (!_restPoseCaptured) yield return null;

        Vector3 up    = Vector3.up;
        Vector3 right = transform.right;
        Vector3 fwd   = transform.forward;

        Vector3 upperArmDir = (up * 4f + right * 0.6f + fwd * 0.4f).normalized;
        Quaternion upperArmTarget = AimBone(
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, upperArmDir);

        Vector3 forearmDir = (up * 3f + fwd * 0.3f + right * 0.2f).normalized;
        Quaternion lowerArmTarget = AimBoneWithPredictedParent(
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            forearmDir, upperArmTarget);

        // Palm correction: orient palm forward/inward using predicted forearm rotation
        Quaternion forearmFutureWorld = PredictForearmWorld(upperArmTarget, lowerArmTarget);
        Vector3 palmDesired = (fwd + Vector3.down * 0.2f).normalized;
        Quaternion handTarget = ComputePalmCorrectedHand(
            HumanBodyBones.RightHand, palmDesired, forearmFutureWorld);

        Quaternion headTarget = Rest(HumanBodyBones.Head) * Quaternion.Euler(-5f, 3f, 0);

        var targets = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.RightUpperArm] = upperArmTarget,
            [HumanBodyBones.RightLowerArm] = lowerArmTarget,
            [HumanBodyBones.RightHand]     = handTarget,
            [HumanBodyBones.Head]          = headTarget,
        };
        yield return StartCoroutine(TransitionTo(targets, 0.6f));

        // Hold with gentle hand sway (applied on top of palm-corrected base)
        float st = 0;
        float endTime = duration > 0 ? duration : 999f;
        while (st < endTime && _behaviorActive)
        {
            float sway = Mathf.Sin(st * 1.8f) * 2f;
            SetOverride(HumanBodyBones.RightHand,
                handTarget * Quaternion.Euler(sway, 0, sway * 0.5f));
            st += Time.deltaTime;
            yield return null;
        }
    }

    /// TAKE NOTES: arm positioned on desk surface, subtle writing motion, periodic glances.
    IEnumerator TakeNotesRoutine(float duration)
    {
        CurrentBehaviorName = "记笔记";
        while (!_restPoseCaptured) yield return null;

        Quaternion headDown = Rest(HumanBodyBones.Head) * Quaternion.Euler(22f, 0, 0);
        Quaternion neckDown = Rest(HumanBodyBones.Neck) * Quaternion.Euler(10f, 0, 0);
        Quaternion headUp   = Rest(HumanBodyBones.Head) * Quaternion.Euler(-5f, 0, 0);
        Quaternion neckUp   = Rest(HumanBodyBones.Neck) * Quaternion.Euler(-3f, 0, 0);

        // Arm positioned to rest on desk: elbow close to body, forearm horizontal
        Quaternion upperArmBase = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(55f, 10f, -12f);
        Quaternion lowerArmBase = Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, -80f, 0);

        var writingPose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Head]          = headDown,
            [HumanBodyBones.Neck]          = neckDown,
            [HumanBodyBones.Spine]         = Rest(HumanBodyBones.Spine) * Quaternion.Euler(8f, 0, 0),
            [HumanBodyBones.RightUpperArm] = upperArmBase,
            [HumanBodyBones.RightLowerArm] = lowerArmBase,
        };
        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            writingPose[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(4f, 0, 0);

        yield return StartCoroutine(TransitionTo(writingPose, 0.5f));

        float t = 0;
        float endTime = duration > 0 ? duration : 999f;
        float glanceTimer = Random.Range(3f, 5f);
        float glanceElapsed = 0f;
        bool  glancing = false;
        const float glanceTrans = 0.35f;
        const float glanceHold  = 0.6f;

        while (t < endTime && _behaviorActive)
        {
            // Subtle writing motion — small wrist wobble + tiny forearm sway
            float writeX = Mathf.Sin(t * 4.5f) * 2.5f + Mathf.Sin(t * 7f) * 1f;
            float writeY = Mathf.Sin(t * 3.0f) * 1.5f;
            SetOverride(HumanBodyBones.RightHand,
                Rest(HumanBodyBones.RightHand) * Quaternion.Euler(writeX, writeY, 0));

            float armOsc = Mathf.Sin(t * 4.2f) * 1.2f;
            SetOverride(HumanBodyBones.RightLowerArm,
                lowerArmBase * Quaternion.Euler(0, armOsc, armOsc * 0.3f));

            // Periodic head-up glance at teacher
            glanceTimer -= Time.deltaTime;
            if (!glancing && glanceTimer <= 0f)
            {
                glancing = true;
                glanceElapsed = 0f;
            }

            if (glancing)
            {
                glanceElapsed += Time.deltaTime;
                float totalGlance = glanceTrans + glanceHold + glanceTrans;
                float lookUp;
                if (glanceElapsed < glanceTrans)
                    lookUp = Mathf.SmoothStep(0, 1, glanceElapsed / glanceTrans);
                else if (glanceElapsed < glanceTrans + glanceHold)
                    lookUp = 1f;
                else if (glanceElapsed < totalGlance)
                    lookUp = 1f - Mathf.SmoothStep(0, 1, (glanceElapsed - glanceTrans - glanceHold) / glanceTrans);
                else
                {
                    lookUp = 0f;
                    glancing = false;
                    glanceTimer = Random.Range(3f, 6f);
                }
                SetOverride(HumanBodyBones.Head, Quaternion.Slerp(headDown, headUp, lookUp));
                SetOverride(HumanBodyBones.Neck, Quaternion.Slerp(neckDown, neckUp, lookUp));
            }
            else
            {
                float breathH = Mathf.Sin(t * 1.2f) * 0.8f;
                SetOverride(HumanBodyBones.Head, headDown * Quaternion.Euler(breathH, 0, 0));
            }

            t += Time.deltaTime;
            yield return null;
        }
    }

    /// SCREAM: head thrown back, arms spread, body shaking
    IEnumerator ScreamRoutine(float duration)
    {
        CurrentBehaviorName = "尖叫";
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

    /// HIT DESK: multi-phase slap with easing, impact vibration, and recoil.
    IEnumerator HitDeskRoutine(float duration)
    {
        CurrentBehaviorName = "拍桌子";
        while (!_restPoseCaptured) yield return null;

        float t = 0;
        int hits = 0;
        int maxHits = Mathf.Max(2, Mathf.RoundToInt(duration / 0.75f));

        while (t < duration && hits < maxHits && _behaviorActive)
        {
            // ── Preparation: body tenses ──
            float prep = 0f;
            while (prep < 0.08f && _behaviorActive)
            {
                float p = prep / 0.08f;
                SetOverride(HumanBodyBones.Spine,
                    Rest(HumanBodyBones.Spine) * Quaternion.Euler(-2f * p, 0, 0));
                prep += Time.deltaTime; t += Time.deltaTime;
                yield return null;
            }

            // ── Lift: arm rises with deceleration (ease-out) ──
            float lift = 0f;
            const float liftDur = 0.22f;
            while (lift < liftDur && _behaviorActive)
            {
                float raw = lift / liftDur;
                float p = 1f - (1f - raw) * (1f - raw); // ease-out quadratic
                SetOverride(HumanBodyBones.RightUpperArm,
                    Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(15f * p, 0, -45f * p));
                SetOverride(HumanBodyBones.RightLowerArm,
                    Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, 20f * p, 0));
                SetOverride(HumanBodyBones.Spine,
                    Rest(HumanBodyBones.Spine) * Quaternion.Euler(-3f, 0, 0));
                lift += Time.deltaTime; t += Time.deltaTime;
                yield return null;
            }

            // ── Strike: arm accelerates downward (ease-in) ──
            float strike = 0f;
            const float strikeDur = 0.10f;
            while (strike < strikeDur && _behaviorActive)
            {
                float raw = strike / strikeDur;
                float p = raw * raw; // ease-in: accelerates into impact
                SetOverride(HumanBodyBones.RightUpperArm,
                    Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(
                        Mathf.Lerp(15f, 55f, p), 0, Mathf.Lerp(-45f, 0f, p)));
                SetOverride(HumanBodyBones.RightLowerArm,
                    Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, Mathf.Lerp(20f, -85f, p), 0));
                SetOverride(HumanBodyBones.Spine,
                    Rest(HumanBodyBones.Spine) * Quaternion.Euler(Mathf.Lerp(-3f, 8f, p), 0, 0));
                SetOverride(HumanBodyBones.Head,
                    Rest(HumanBodyBones.Head) * Quaternion.Euler(4f * p, 0, 0));
                strike += Time.deltaTime; t += Time.deltaTime;
                yield return null;
            }

            // ── Impact: freeze + high-frequency vibration ──
            float impact = 0f;
            const float impactDur = 0.10f;
            while (impact < impactDur && _behaviorActive)
            {
                float decay = 1f - impact / impactDur;
                float shake = Mathf.Sin(impact * 55f) * 2.5f * decay;
                SetOverride(HumanBodyBones.RightHand,
                    Rest(HumanBodyBones.RightHand) * Quaternion.Euler(shake, shake * 0.4f, 0));
                SetOverride(HumanBodyBones.Spine,
                    Rest(HumanBodyBones.Spine) * Quaternion.Euler(8f, shake * 0.3f, 0));
                impact += Time.deltaTime; t += Time.deltaTime;
                yield return null;
            }

            // ── Recoil: small bounce-back ──
            float recoil = 0f;
            const float recoilDur = 0.15f;
            while (recoil < recoilDur && _behaviorActive)
            {
                float p = Mathf.SmoothStep(0, 1, recoil / recoilDur);
                SetOverride(HumanBodyBones.RightUpperArm,
                    Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(
                        Mathf.Lerp(55f, 30f, p), 0, Mathf.Lerp(0, -15f, p)));
                SetOverride(HumanBodyBones.RightLowerArm,
                    Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, Mathf.Lerp(-85f, -35f, p), 0));
                SetOverride(HumanBodyBones.Spine,
                    Rest(HumanBodyBones.Spine) * Quaternion.Euler(Mathf.Lerp(8f, 2f, p), 0, 0));
                SetOverride(HumanBodyBones.Head,
                    Rest(HumanBodyBones.Head) * Quaternion.Euler(Mathf.Lerp(4f, 0f, p), 0, 0));
                recoil += Time.deltaTime; t += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.08f);
            t += 0.08f;
            hits++;
        }
    }

    /// PUSH CLASSMATE: wind-up → lean → accelerating thrust → recovery.
    IEnumerator PushClassmateRoutine(Transform target, float duration)
    {
        CurrentBehaviorName = "推同学";
        while (!_restPoseCaptured) yield return null;

        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.right;
        dir.y = 0; dir.Normalize();

        float yAngle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);
        float yaw = Mathf.Clamp(yAngle, -60f, 60f);

        // Phase 1 — Wind-up: lean back, tense arms
        var windUp = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(-5f, yaw * 0.3f, 0),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(25f, 0, -25f),
            [HumanBodyBones.LeftUpperArm]  = Rest(HumanBodyBones.LeftUpperArm)  * Quaternion.Euler(25f, 0, 25f),
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(0, yaw * 0.4f, 0),
        };
        yield return StartCoroutine(TransitionTo(windUp, 0.2f));

        // Phase 2 — Lean + extend arms toward target
        var leanPose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(22f, yaw * 0.5f, 0),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) * Quaternion.Euler(55f, yaw * 0.3f, -18f),
            [HumanBodyBones.RightLowerArm] = Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, -50f, 0),
            [HumanBodyBones.LeftUpperArm]  = Rest(HumanBodyBones.LeftUpperArm)  * Quaternion.Euler(55f, yaw * 0.3f, 18f),
            [HumanBodyBones.LeftLowerArm]  = Rest(HumanBodyBones.LeftLowerArm)  * Quaternion.Euler(0, 50f, 0),
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(0, yaw * 0.5f, 0),
        };
        yield return StartCoroutine(TransitionTo(leanPose, 0.15f));

        // Phase 3 — Push thrust (ease-in acceleration)
        float thrust = 0f;
        const float thrustDur = 0.18f;
        while (thrust < thrustDur && _behaviorActive)
        {
            float raw = thrust / thrustDur;
            float p = raw * raw;
            SetOverride(HumanBodyBones.Spine,
                Rest(HumanBodyBones.Spine) * Quaternion.Euler(Mathf.Lerp(22f, 32f, p), yaw * 0.7f, 0));
            SetOverride(HumanBodyBones.RightLowerArm,
                Rest(HumanBodyBones.RightLowerArm) * Quaternion.Euler(0, Mathf.Lerp(-50f, -12f, p), 0));
            SetOverride(HumanBodyBones.LeftLowerArm,
                Rest(HumanBodyBones.LeftLowerArm) * Quaternion.Euler(0, Mathf.Lerp(50f, 12f, p), 0));
            thrust += Time.deltaTime;
            yield return null;
        }

        // Phase 4 — Recovery
        yield return new WaitForSeconds(0.15f);
        var recovery = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Spine]         = Rest(HumanBodyBones.Spine) * Quaternion.Euler(3f, 0, 0),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm),
            [HumanBodyBones.RightLowerArm] = Rest(HumanBodyBones.RightLowerArm),
            [HumanBodyBones.LeftUpperArm]  = Rest(HumanBodyBones.LeftUpperArm),
            [HumanBodyBones.LeftLowerArm]  = Rest(HumanBodyBones.LeftLowerArm),
            [HumanBodyBones.Head]          = Rest(HumanBodyBones.Head),
        };
        yield return StartCoroutine(TransitionTo(recovery, 0.35f));

        yield return new WaitForSeconds(Mathf.Max(0, duration - 1.1f));
    }

    /// LIE DOWN / SLUMP: continuous shallow desk slump with visible hands.
    IEnumerator LieDownRoutine(float duration)
    {
        CurrentBehaviorName = "趴桌";

        while (!_restPoseCaptured) yield return null;

        int variant = Random.Range(0, 3);
        float side = Random.value > 0.5f ? 1f : -1f;
        float yaw = variant == 2 ? side * Random.Range(5f, 8f) : Random.Range(-3f, 4f);
        float roll = variant == 2 ? side * Random.Range(3f, 6f) : Random.Range(-2f, 3f);
        float entrySeconds = Random.Range(1.45f, 1.85f);
        float exitSeconds = Random.Range(0.9f, 1.2f);
        bool holdUntilRecovery = duration <= 0f;
        float totalDuration = duration > 0 ? duration : 0f;
        float holdSeconds = holdUntilRecovery ? 0f : Mathf.Max(0.4f, totalDuration - entrySeconds - exitSeconds);

        DeskSlumpTargets targets = FindLieDownDeskTargets(variant, side);

        float t = 0f;
        while (t < entrySeconds && _behaviorActive)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / entrySeconds);
            float armDelay = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, p));
            ApplyLieDownPose(
                variant,
                side,
                yaw,
                roll,
                p,
                0f,
                0f,
                armDelay,
                targets);
            t += Time.deltaTime;
            yield return null;
        }

        float shiftSeed = Random.Range(0f, 6.28f);
        t = 0f;
        while (_behaviorActive && (holdUntilRecovery ? !_lieDownExitRequested : t < holdSeconds))
        {
            float breath = Mathf.Sin(t * 1.25f + shiftSeed) * 0.65f;
            float tinyShift = Mathf.Sin(t * 0.62f + shiftSeed) * 0.45f;
            float handFidget = Mathf.Sin(t * 1.8f + shiftSeed) * 0.8f;
            ApplyLieDownPose(
                variant,
                side,
                yaw + tinyShift,
                roll + tinyShift * 0.35f,
                1f,
                breath,
                handFidget,
                1f,
                targets);
            t += Time.deltaTime;
            yield return null;
        }

        if (!_behaviorActive) yield break;

        t = 0f;
        while (t < exitSeconds && _behaviorActive)
        {
            float p = 1f - Mathf.SmoothStep(0f, 1f, t / exitSeconds);
            ApplyLieDownPose(
                variant,
                side,
                yaw * p,
                roll * p,
                p,
                0f,
                0f,
                p,
                targets);
            t += Time.deltaTime;
            yield return null;
        }
    }

    Dictionary<HumanBodyBones, Quaternion> BuildLieDownPose(int variant, float side, float yaw, float roll, float intensity)
    {
        float finalSpine = variant == 0 ? 16f : (variant == 1 ? 20f : 18f);
        float finalChest = variant == 0 ? 9f : (variant == 1 ? 12f : 11f);
        float finalNeck = variant == 0 ? 9f : (variant == 1 ? 12f : 11f);
        float finalHead = variant == 0 ? 18f : (variant == 1 ? 21f : 20f);

        float forward = Mathf.Lerp(0f, finalSpine, intensity);
        float chestForward = Mathf.Lerp(0f, finalChest, intensity);
        float neckForward = Mathf.Lerp(0f, finalNeck, intensity);
        float headForward = Mathf.Lerp(0f, finalHead, intensity);

        var pose = new Dictionary<HumanBodyBones, Quaternion> {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(forward, yaw * 0.25f, roll * 0.2f),
            [HumanBodyBones.Neck] = Rest(HumanBodyBones.Neck) * Quaternion.Euler(neckForward, yaw * 0.45f, roll * 0.35f),
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) * Quaternion.Euler(headForward, yaw, roll),
        };

        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            pose[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(chestForward, yaw * 0.18f, roll * 0.12f);
        if (_restPose.ContainsKey(HumanBodyBones.UpperChest))
            pose[HumanBodyBones.UpperChest] = Rest(HumanBodyBones.UpperChest) * Quaternion.Euler(chestForward * 0.65f, yaw * 0.12f, roll * 0.1f);

        return pose;
    }

    void ApplyLieDownPose(
        int variant,
        float side,
        float yaw,
        float roll,
        float intensity,
        float breath,
        float handFidget,
        float armWeight,
        DeskSlumpTargets targets)
    {
        Dictionary<HumanBodyBones, Quaternion> pose = BuildLieDownPose(variant, side, yaw, roll, 1f);
        foreach (var kv in pose)
        {
            SetOverride(kv.Key, kv.Value, intensity);
        }

        float spineBase = variant == 0 ? 16f : (variant == 1 ? 20f : 18f);
        float chestBase = variant == 0 ? 9f : (variant == 1 ? 12f : 11f);
        SetOverride(HumanBodyBones.Spine,
            Rest(HumanBodyBones.Spine) * Quaternion.Euler((spineBase + breath) * intensity, yaw * 0.25f, roll * 0.2f), intensity);
        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            SetOverride(HumanBodyBones.Chest,
                Rest(HumanBodyBones.Chest) * Quaternion.Euler((chestBase + breath * 0.45f) * intensity, yaw * 0.18f, roll * 0.12f), intensity);

        Quaternion leftUpperArmTarget;
        Quaternion rightUpperArmTarget;
        Quaternion leftLowerArmTarget;
        Quaternion rightLowerArmTarget;
        Vector3 leftBendHint = -targets.right * 0.7f + Vector3.up * 0.14f + targets.forward * 0.18f;
        Vector3 rightBendHint = targets.right * 0.62f + Vector3.up * 0.12f + targets.forward * 0.22f;

        ComputeArmIKToTarget(
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            targets.leftHand,
            leftBendHint,
            out leftUpperArmTarget,
            out leftLowerArmTarget);
        ComputeArmIKToTarget(
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            targets.rightHand,
            rightBendHint,
            out rightUpperArmTarget,
            out rightLowerArmTarget);

        SetOverride(HumanBodyBones.LeftUpperArm, leftUpperArmTarget, armWeight);
        SetOverride(HumanBodyBones.RightUpperArm, rightUpperArmTarget, armWeight);
        SetOverride(HumanBodyBones.LeftLowerArm, leftLowerArmTarget, armWeight);
        SetOverride(HumanBodyBones.RightLowerArm, rightLowerArmTarget, armWeight);

        Quaternion leftForearmWorld = PredictForearmWorld(
            HumanBodyBones.LeftUpperArm,
            leftUpperArmTarget,
            leftLowerArmTarget);
        Quaternion rightForearmWorld = PredictForearmWorld(
            HumanBodyBones.RightUpperArm,
            rightUpperArmTarget,
            rightLowerArmTarget);

        Vector3 leftPalmDir = (Vector3.down * 1.8f + targets.forward * 0.15f + targets.right * 0.08f).normalized;
        Vector3 rightPalmDir = (Vector3.down * 2f - targets.forward * 0.06f - targets.right * 0.05f).normalized;
        Quaternion leftHandTarget = ComputePalmCorrectedHand(HumanBodyBones.LeftHand, leftPalmDir, leftForearmWorld)
            * Quaternion.Euler(handFidget * 0.35f, -4f, 6f + handFidget * 0.2f);
        Quaternion rightHandTarget = ComputePalmCorrectedHand(HumanBodyBones.RightHand, rightPalmDir, rightForearmWorld)
            * Quaternion.Euler(-handFidget * 0.25f, 3f, -4f + handFidget * -0.15f);

        SetOverride(HumanBodyBones.LeftHand, leftHandTarget, armWeight);
        SetOverride(HumanBodyBones.RightHand, rightHandTarget, armWeight);
    }

    DeskSlumpTargets FindLieDownDeskTargets(int variant, float side)
    {
        Vector3 chest = transform.position + Vector3.up * 0.95f;
        Transform chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chestBone != null)
        {
            chest = chestBone.position;
        }

        Renderer bestRenderer = null;
        Bounds bestBounds = default;
        float bestScore = float.MaxValue;
        Renderer[] renderers = FindObjectsOfType<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.transform.IsChildOf(transform)) continue;

            Bounds bounds = renderer.bounds;
            Vector3 toCenter = bounds.center - chest;
            Vector3 flat = Vector3.ProjectOnPlane(toCenter, Vector3.up);
            float forwardDistance = Vector3.Dot(flat, transform.forward);
            float sideDistance = Mathf.Abs(Vector3.Dot(flat, transform.right));
            float topY = bounds.max.y;
            Vector3 size = bounds.size;

            if (forwardDistance < 0.2f || forwardDistance > 2.4f) continue;
            if (sideDistance > 1.45f) continue;
            if (topY < 0.45f || topY > 1.2f) continue;
            if (size.x < 0.35f || size.z < 0.25f) continue;

            float score = forwardDistance + sideDistance * 0.7f + Mathf.Abs(topY - 0.75f) * 0.8f;
            if (score < bestScore)
            {
                bestScore = score;
                bestRenderer = renderer;
                bestBounds = bounds;
            }
        }

        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        Vector3 nearPoint = transform.position + fwd * 0.72f;
        float surfaceY = transform.position.y + 0.76f;

        if (bestRenderer != null)
        {
            surfaceY = bestBounds.max.y;
            Vector3 flatToDesk = Vector3.ProjectOnPlane(bestBounds.center - chest, Vector3.up);
            if (flatToDesk.sqrMagnitude > 0.0001f)
            {
                fwd = flatToDesk.normalized;
                right = Vector3.Cross(Vector3.up, fwd).normalized;
            }

            nearPoint = bestBounds.ClosestPoint(chest);
            nearPoint += fwd * 0.14f;
        }

        float wristY = surfaceY + 0.06f;
        nearPoint.y = wristY;

        float leftSpread = variant == 1 ? 0.11f : 0.13f;
        float rightSpread = variant == 1 ? 0.17f : 0.15f;
        float sideBias = variant == 2 ? side * 0.025f : 0f;
        Vector3 leftRestOffset = fwd * 0.08f - right * 0.015f + right * sideBias;
        Vector3 rightRestOffset = fwd * (variant == 1 ? 0.22f : 0.17f) + right * sideBias;

        DeskSlumpTargets targets = new DeskSlumpTargets
        {
            foundDesk = bestRenderer != null,
            forward = fwd,
            right = right,
            surfaceY = surfaceY,
            leftHand = nearPoint - right * leftSpread + leftRestOffset,
            rightHand = nearPoint + right * rightSpread + rightRestOffset
        };

        return targets;
    }

    void ComputeArmIKToTarget(
        HumanBodyBones upperBone,
        HumanBodyBones lowerBone,
        HumanBodyBones handBone,
        Vector3 target,
        float outwardSign,
        out Quaternion upperLocal,
        out Quaternion lowerLocal)
    {
        Vector3 bendHint = transform.right * outwardSign * 0.85f + Vector3.down * 0.25f + transform.forward * 0.42f;
        ComputeArmIKToTarget(
            upperBone,
            lowerBone,
            handBone,
            target,
            bendHint,
            out upperLocal,
            out lowerLocal);
    }

    void ComputeArmIKToTarget(
        HumanBodyBones upperBone,
        HumanBodyBones lowerBone,
        HumanBodyBones handBone,
        Vector3 target,
        Vector3 bendHint,
        out Quaternion upperLocal,
        out Quaternion lowerLocal)
    {
        Transform upper = animator.GetBoneTransform(upperBone);
        Transform lower = animator.GetBoneTransform(lowerBone);
        Transform hand = animator.GetBoneTransform(handBone);
        if (upper == null || lower == null || hand == null)
        {
            upperLocal = Rest(upperBone);
            lowerLocal = Rest(lowerBone);
            return;
        }

        Vector3 shoulder = upper.position;
        float upperLen = Mathf.Max(0.05f, Vector3.Distance(upper.position, lower.position));
        float lowerLen = Mathf.Max(0.05f, Vector3.Distance(lower.position, hand.position));
        Vector3 shoulderToTarget = target - shoulder;
        float distance = Mathf.Clamp(shoulderToTarget.magnitude, 0.08f, upperLen + lowerLen - 0.02f);
        Vector3 targetDir = shoulderToTarget.sqrMagnitude > 0.0001f
            ? shoulderToTarget.normalized
            : transform.forward;

        float a = Mathf.Clamp((upperLen * upperLen - lowerLen * lowerLen + distance * distance) / (2f * distance), 0.02f, upperLen - 0.01f);
        float h = Mathf.Sqrt(Mathf.Max(0.0001f, upperLen * upperLen - a * a));
        Vector3 bendDir = Vector3.ProjectOnPlane(bendHint, targetDir);
        if (bendDir.sqrMagnitude < 0.0001f)
        {
            bendDir = Vector3.ProjectOnPlane(Vector3.down, targetDir);
        }
        bendDir.Normalize();

        Vector3 elbow = shoulder + targetDir * a + bendDir * h;
        Vector3 upperDir = (elbow - shoulder).normalized;
        Vector3 lowerDir = (target - elbow).normalized;

        upperLocal = AimBone(upperBone, lowerBone, upperDir);
        lowerLocal = AimBoneWithPredictedParent(lowerBone, handBone, lowerDir, upperLocal);
    }

    /// TOUCH NOSE: DTT instruction response with coordinated head, torso, and right arm.
    IEnumerator TouchNoseRoutine(float duration)
    {
        CurrentBehaviorName = "摸鼻子";
        while (!_restPoseCaptured) yield return null;

        float entrySeconds = Random.Range(0.72f, 0.9f);
        float exitSeconds = Random.Range(0.55f, 0.7f);
        float totalDuration = Mathf.Max(2f, duration);
        float holdSeconds = Mathf.Max(0.35f, totalDuration - entrySeconds - exitSeconds);
        float headYaw = Random.Range(-1.5f, 1.5f);
        float wristSeed = Random.Range(0f, 6.28f);

        float t = 0f;
        while (t < entrySeconds && _behaviorActive)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / entrySeconds);
            ApplyTouchNosePose(p, headYaw, 0f);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < holdSeconds && _behaviorActive)
        {
            float tiny = Mathf.Sin(t * 2.2f + wristSeed) * 0.8f;
            float nod = Mathf.Sin(t * 1.4f + wristSeed) * 0.55f;
            ApplyTouchNosePose(1f, headYaw + nod, tiny);
            t += Time.deltaTime;
            yield return null;
        }

        if (!_behaviorActive) yield break;

        t = 0f;
        while (t < exitSeconds && _behaviorActive)
        {
            float p = 1f - Mathf.SmoothStep(0f, 1f, t / exitSeconds);
            ApplyTouchNosePose(p, headYaw * p, 0f);
            t += Time.deltaTime;
            yield return null;
        }
    }

    void ApplyTouchNosePose(float weight, float headYaw, float wristFidget)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Vector3 faceForward = GetHeadFaceForward(head);
        Vector3 right = transform.right;
        Vector3 up = Vector3.up;
        Vector3 faceContact = head.position
            + right * touchNoseFaceContactOffset.x
            + up * touchNoseFaceContactOffset.y
            + faceForward * touchNoseFaceContactOffset.z;

        Vector3 wristTarget = head.position
            + right * touchNoseWristOffset.x
            + up * touchNoseWristOffset.y
            + faceForward * touchNoseWristOffset.z;
        wristTarget = KeepPointOutsideHead(wristTarget, head.position, faceForward, 0.07f, 0.04f);

        Quaternion rightUpperArmTarget;
        Quaternion rightLowerArmTarget;
        ComputeArmIKToTarget(
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            wristTarget,
            right * 0.75f + Vector3.down * 0.45f - faceForward * 0.08f,
            out rightUpperArmTarget,
            out rightLowerArmTarget);

        Quaternion forearmFutureWorld = PredictForearmWorld(rightUpperArmTarget, rightLowerArmTarget);
        Vector3 palmToFace = (faceContact - wristTarget).normalized;
        if (palmToFace.sqrMagnitude < 0.001f)
        {
            palmToFace = -faceForward;
        }
        Quaternion handTarget = ComputePalmCorrectedHand(HumanBodyBones.RightHand, palmToFace, forearmFutureWorld);
        handTarget *= Quaternion.Euler(0f, -4f + wristFidget * 0.2f, 3f);

        SetOverride(HumanBodyBones.Spine,
            Rest(HumanBodyBones.Spine) * Quaternion.Euler(3f, headYaw * 0.16f, 0f), weight);
        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            SetOverride(HumanBodyBones.Chest,
                Rest(HumanBodyBones.Chest) * Quaternion.Euler(2f, headYaw * 0.2f, 0f), weight);
        if (_restPose.ContainsKey(HumanBodyBones.UpperChest))
            SetOverride(HumanBodyBones.UpperChest,
                Rest(HumanBodyBones.UpperChest) * Quaternion.Euler(1.5f, headYaw * 0.16f, 0f), weight);

        SetOverride(HumanBodyBones.Neck,
            Rest(HumanBodyBones.Neck) * Quaternion.Euler(1.5f, headYaw * 0.3f, 0f), weight);
        SetOverride(HumanBodyBones.Head,
            Rest(HumanBodyBones.Head) * Quaternion.Euler(2.5f, headYaw, 0f), weight);
        SetOverride(HumanBodyBones.RightUpperArm, rightUpperArmTarget, weight);
        SetOverride(HumanBodyBones.RightLowerArm, rightLowerArmTarget, weight);
        SetOverride(HumanBodyBones.RightHand, handTarget, weight);
    }

    Vector3 GetHeadFaceForward(Transform head)
    {
        Vector3 faceForward = head.forward;
        if (Vector3.Dot(faceForward, transform.forward) < 0f)
        {
            faceForward = -faceForward;
        }

        faceForward.y = Mathf.Clamp(faceForward.y, -0.35f, 0.35f);
        if (faceForward.sqrMagnitude < 0.0001f)
        {
            faceForward = transform.forward;
        }

        return faceForward.normalized;
    }

    Vector3 KeepPointOutsideHead(Vector3 point, Vector3 headCenter, Vector3 faceForward, float minForward, float minRadius)
    {
        Vector3 offset = point - headCenter;
        float forwardDistance = Vector3.Dot(offset, faceForward);
        if (forwardDistance < minForward)
        {
            point += faceForward * (minForward - forwardDistance);
            offset = point - headCenter;
        }

        Vector3 lateral = Vector3.ProjectOnPlane(offset, faceForward);
        if (lateral.magnitude < minRadius)
        {
            Vector3 pushDir = lateral.sqrMagnitude > 0.0001f ? lateral.normalized : transform.right;
            point += pushDir * (minRadius - lateral.magnitude);
        }

        return point;
    }

    /// SPEAKING MOTION: natural head movement with varied rhythm
    IEnumerator SpeakingMotionRoutine(float duration)
    {
        CurrentBehaviorName = "说话";
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

    /// REACTION: generic flinch (legacy fallback — use PlayPushedReaction for pushes).
    IEnumerator ReactionRoutine(string type, float duration)
    {
        CurrentBehaviorName = "反应";
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

    /// PUSHED REACTION: directional stumble with displacement + recovery.
    IEnumerator PushedReactionRoutine(Vector3 pushDir, float duration)
    {
        CurrentBehaviorName = "被推";
        while (!_restPoseCaptured) yield return null;

        pushDir.y = 0;
        if (pushDir.sqrMagnitude < 0.001f) pushDir = -transform.forward;
        pushDir.Normalize();

        float pushAngle = Vector3.SignedAngle(transform.forward, pushDir, Vector3.up);
        float leanSide = Mathf.Clamp(pushAngle / 90f, -1f, 1f);

        // Phase 1 — Impact flinch (fast)
        var impactPose = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) *
                Quaternion.Euler(-5f, 0, leanSide * 18f),
            [HumanBodyBones.Head] = Rest(HumanBodyBones.Head) *
                Quaternion.Euler(0, 0, leanSide * 14f),
            [HumanBodyBones.LeftUpperArm] = Rest(HumanBodyBones.LeftUpperArm) *
                Quaternion.Euler(0, 0, 25f),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm) *
                Quaternion.Euler(0, 0, -25f),
        };
        yield return StartCoroutine(TransitionTo(impactPose, 0.1f));

        // Phase 2 — Displacement: slide smoothly in push direction
        Vector3 startPos = transform.position;
        Vector3 displaceTarget = startPos + pushDir * 0.3f;
        float slideT = 0f;
        const float slideDur = 0.3f;
        while (slideT < slideDur && _behaviorActive)
        {
            float p = Mathf.SmoothStep(0, 1, slideT / slideDur);
            transform.position = Vector3.Lerp(startPos, displaceTarget, p);

            float wobble = Mathf.Sin(slideT * 22f) * 3f * (1f - slideT / slideDur);
            SetOverride(HumanBodyBones.Spine,
                impactPose[HumanBodyBones.Spine] * Quaternion.Euler(wobble, 0, 0));
            slideT += Time.deltaTime;
            yield return null;
        }
        transform.position = displaceTarget;

        // Phase 3 — Recovery to slightly off-balance stance
        var recovery = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine]         = Rest(HumanBodyBones.Spine) * Quaternion.Euler(4f, 0, leanSide * 3f),
            [HumanBodyBones.Head]          = Rest(HumanBodyBones.Head),
            [HumanBodyBones.LeftUpperArm]  = Rest(HumanBodyBones.LeftUpperArm),
            [HumanBodyBones.RightUpperArm] = Rest(HumanBodyBones.RightUpperArm),
        };
        yield return StartCoroutine(TransitionTo(recovery, 0.45f));

        float holdT = 0f;
        float holdDur = Mathf.Max(0, duration - 0.9f);
        while (holdT < holdDur && _behaviorActive)
        {
            float breath = Mathf.Sin(holdT * 2f) * 1.5f;
            SetOverride(HumanBodyBones.Spine,
                recovery[HumanBodyBones.Spine] * Quaternion.Euler(breath, 0, 0));
            holdT += Time.deltaTime;
            yield return null;
        }
    }

    /// LISTEN TO CLASSMATE: face speaker using head bone targeting + attentive nod.
    IEnumerator ListenToClassmateRoutine(Transform speaker, float duration)
    {
        CurrentBehaviorName = "听同学说话";
        while (!_restPoseCaptured) yield return null;

        ComputeFaceTargetAngles(speaker, out float yaw, out float pitch);
        yaw = Mathf.Clamp(yaw, -85f, 85f);
        pitch = Mathf.Clamp(pitch, -15f, 15f);

        bool hasChest = _restPose.ContainsKey(HumanBodyBones.Chest);
        float spineF = hasChest ? 0.15f : 0.20f;
        float chestF = hasChest ? 0.10f : 0f;
        float neckF  = hasChest ? 0.30f : 0.35f;
        float headF  = 1f - spineF - chestF - neckF;

        var listenPose = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f, yaw * spineF, 0),
            [HumanBodyBones.Neck]  = Rest(HumanBodyBones.Neck)  * Quaternion.Euler(pitch * 0.35f, yaw * neckF, 0),
            [HumanBodyBones.Head]  = Rest(HumanBodyBones.Head)  * Quaternion.Euler(pitch * 0.65f, yaw * headF, 0),
        };
        if (hasChest)
            listenPose[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(0, yaw * chestF, 0);

        yield return StartCoroutine(TransitionTo(listenPose, 0.5f));

        float t = 0f, endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            float nod  = Mathf.Sin(t * 1.8f) * 2.5f + Mathf.Sin(t * 3.2f) * 1.2f;
            float tilt = Mathf.Sin(t * 0.9f) * 1.5f;

            SetOverride(HumanBodyBones.Head,
                Rest(HumanBodyBones.Head) * Quaternion.Euler(
                    pitch * 0.65f + nod, yaw * headF + tilt, 0));
            SetOverride(HumanBodyBones.Neck,
                Rest(HumanBodyBones.Neck) * Quaternion.Euler(
                    pitch * 0.35f + nod * 0.2f, yaw * neckF, 0));

            t += Time.deltaTime;
            yield return null;
        }
    }

    /// ASK QUESTION: eager hand raise + forward lean + palm correction.
    IEnumerator AskQuestionRoutine(float duration)
    {
        CurrentBehaviorName = "举手提问";
        while (!_restPoseCaptured) yield return null;

        Vector3 up    = Vector3.up;
        Vector3 right = transform.right;
        Vector3 fwd   = transform.forward;

        Vector3 upperArmDir = (up * 4f + right * 0.7f + fwd * 0.5f).normalized;
        Quaternion upperArmTarget = AimBone(
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, upperArmDir);

        Vector3 forearmDir = (up * 2.5f + fwd * 0.6f + right * 0.3f).normalized;
        Quaternion lowerArmTarget = AimBoneWithPredictedParent(
            HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            forearmDir, upperArmTarget);

        // Palm faces forward/inward
        Quaternion forearmFutureWorld = PredictForearmWorld(upperArmTarget, lowerArmTarget);
        Vector3 palmDesired = (fwd + Vector3.down * 0.15f).normalized;
        Quaternion handTarget = ComputePalmCorrectedHand(
            HumanBodyBones.RightHand, palmDesired, forearmFutureWorld);

        var targets = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.RightUpperArm] = upperArmTarget,
            [HumanBodyBones.RightLowerArm] = lowerArmTarget,
            [HumanBodyBones.RightHand]     = handTarget,
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(10f, 0, 0),
            [HumanBodyBones.Neck]  = Rest(HumanBodyBones.Neck)  * Quaternion.Euler(-8f, 0, 0),
            [HumanBodyBones.Head]  = Rest(HumanBodyBones.Head)  * Quaternion.Euler(-12f, 4f, 0),
        };
        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            targets[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(5f, 0, 0);

        yield return StartCoroutine(TransitionTo(targets, 0.5f));

        // Hold — impatient wave on top of palm-corrected base + eager nod
        float t = 0f, endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            float wave = Mathf.Sin(t * 3.5f) * 3f;
            float nod  = Mathf.Sin(t * 2.2f) * 2.5f;
            SetOverride(HumanBodyBones.RightHand,
                handTarget * Quaternion.Euler(wave, wave * 0.4f, 0));
            SetOverride(HumanBodyBones.Head,
                targets[HumanBodyBones.Head] * Quaternion.Euler(nod, 0, 0));
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// DISTRACTED: slouch, look away, idle fidget
    IEnumerator DistractedRoutine(float duration)
    {
        CurrentBehaviorName = "走神";
        while (!_restPoseCaptured) yield return null;

        // Randomise which side the student looks toward
        float lookSide = Random.value > 0.5f ? 1f : -1f;

        var slouch = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(-4f, lookSide * 12f, lookSide * 2f),
            [HumanBodyBones.Neck]  = Rest(HumanBodyBones.Neck)  * Quaternion.Euler(-6f, lookSide * 18f, 0),
            [HumanBodyBones.Head]  = Rest(HumanBodyBones.Head)  * Quaternion.Euler(-8f, lookSide * 22f, 0),
        };
        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            slouch[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(-3f, lookSide * 5f, 0);

        yield return StartCoroutine(TransitionTo(slouch, 0.9f));

        float t = 0f, endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            // Slow Perlin-noise look drift — feels organic
            float lookDrift = (Mathf.PerlinNoise(t * 0.4f, 0f) * 2f - 1f) * 15f * lookSide;
            float headBob   = Mathf.Sin(t * 0.7f) * 1.8f;

            SetOverride(HumanBodyBones.Head,
                Rest(HumanBodyBones.Head) * Quaternion.Euler(-8f + headBob, lookDrift, 0));
            SetOverride(HumanBodyBones.Neck,
                Rest(HumanBodyBones.Neck) * Quaternion.Euler(-4f, lookDrift * 0.55f, 0));

            // Slight right-hand fidget
            float fidget = Mathf.Sin(t * 4.2f) * 2.5f;
            SetOverride(HumanBodyBones.RightHand,
                Rest(HumanBodyBones.RightHand) * Quaternion.Euler(fidget, 0, fidget * 0.3f));

            t += Time.deltaTime;
            yield return null;
        }
    }

    /// TALK TO CLASSMATE: face neighbor using head bone targeting + speaking nod.
    IEnumerator TalkToClassmateRoutine(Transform neighbor, float duration)
    {
        CurrentBehaviorName = "和同学说话";
        while (!_restPoseCaptured) yield return null;

        ComputeFaceTargetAngles(neighbor, out float yaw, out float pitch);
        yaw = Mathf.Clamp(yaw, -85f, 85f);
        pitch = Mathf.Clamp(pitch, -15f, 15f);

        bool hasChest = _restPose.ContainsKey(HumanBodyBones.Chest);
        float spineF = hasChest ? 0.15f : 0.20f;
        float chestF = hasChest ? 0.10f : 0f;
        float neckF  = hasChest ? 0.30f : 0.35f;
        float headF  = 1f - spineF - chestF - neckF;

        var turnPose = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(0, yaw * spineF, 0),
            [HumanBodyBones.Neck]  = Rest(HumanBodyBones.Neck)  * Quaternion.Euler(pitch * 0.35f, yaw * neckF, 0),
            [HumanBodyBones.Head]  = Rest(HumanBodyBones.Head)  * Quaternion.Euler(pitch * 0.65f, yaw * headF, 0),
        };
        if (hasChest)
            turnPose[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(0, yaw * chestF, 0);

        yield return StartCoroutine(TransitionTo(turnPose, 0.5f));

        float t = 0f, endTime = duration > 0 ? duration : 999f;
        while (t < endTime && _behaviorActive)
        {
            float nod   = Mathf.Sin(t * 2.8f) * 3f + Mathf.Sin(t * 4.5f) * 1.5f;
            float tilt  = Mathf.Sin(t * 1.4f) * 2f;
            float emph  = Mathf.Clamp01(Mathf.Sin(t * 1.1f)) * Mathf.Sin(t * 6f) * 2f;

            SetOverride(HumanBodyBones.Head,
                Rest(HumanBodyBones.Head) * Quaternion.Euler(
                    pitch * 0.65f + nod + emph, yaw * headF + tilt, 0));
            SetOverride(HumanBodyBones.Neck,
                Rest(HumanBodyBones.Neck) * Quaternion.Euler(
                    pitch * 0.35f + nod * 0.3f, yaw * neckF, 0));
            SetOverride(HumanBodyBones.Spine,
                Rest(HumanBodyBones.Spine) * Quaternion.Euler(
                    Mathf.Sin(t * 0.8f) * 1.2f, yaw * spineF, 0));

            t += Time.deltaTime;
            yield return null;
        }
    }

    /// LEAVE SEAT: upright stand-up → turn → walk with coordinated limbs → idle.
    IEnumerator LeaveSeatRoutine(Vector3 targetPos, float moveDuration, AnimationClip layingHoldClip, float layingRootYOffset)
    {
        CurrentBehaviorName = "离座";
        while (!_restPoseCaptured) yield return null;
        yield return null;

        ComputeStandingLegPose(out var sLU, out var sRU, out var sLL, out var sRL);
        ComputeHangingArmPose(out var aLU, out var aRU, out var aLL, out var aRL,
            out var swingL, out var swingR);

        // ── Phase 1: Stand up to proper upright posture ──
        var standPose = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine]         = Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f, 0, 0),
            [HumanBodyBones.Neck]          = Rest(HumanBodyBones.Neck),
            [HumanBodyBones.Head]          = Rest(HumanBodyBones.Head),
            [HumanBodyBones.LeftUpperLeg]  = sLU,
            [HumanBodyBones.RightUpperLeg] = sRU,
            [HumanBodyBones.LeftLowerLeg]  = sLL,
            [HumanBodyBones.RightLowerLeg] = sRL,
            [HumanBodyBones.LeftUpperArm]  = aLU,
            [HumanBodyBones.RightUpperArm] = aRU,
            [HumanBodyBones.LeftLowerArm]  = aLL,
            [HumanBodyBones.RightLowerArm] = aRL,
        };
        if (_restPose.ContainsKey(HumanBodyBones.Chest))
            standPose[HumanBodyBones.Chest] = Rest(HumanBodyBones.Chest) * Quaternion.Euler(1f, 0, 0);
        if (_restPose.ContainsKey(HumanBodyBones.UpperChest))
            standPose[HumanBodyBones.UpperChest] = Rest(HumanBodyBones.UpperChest);

        yield return StartCoroutine(TransitionTo(standPose, 0.6f));

        // ── Phase 2: Rotate root toward walk target ──
        Vector3 moveDir = (targetPos - transform.position);
        moveDir.y = 0;
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
            float rotElapsed = 0f;
            Quaternion startRot = transform.rotation;
            while (rotElapsed < 0.35f)
            {
                transform.rotation = Quaternion.Slerp(startRot, targetRot,
                    Mathf.SmoothStep(0, 1, rotElapsed / 0.35f));
                rotElapsed += Time.deltaTime;
                yield return null;
            }
            transform.rotation = targetRot;
        }

        // ── Phase 3: Walk with procedural locomotion (upright throughout) ──
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        moveDuration = Mathf.Max(0.5f, moveDuration);
        const float stepFreq = 4.5f;
        const float legSwing = 20f;
        const float kneeBend = 35f;
        const float armSwingDeg = 20f;
        const float elbowBendDeg = 15f;

        while (elapsed < moveDuration && _behaviorActive)
        {
            float p = Mathf.SmoothStep(0, 1, elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, p);

            float cycle = elapsed * stepFreq;
            float leftStep  = Mathf.Sin(cycle);
            float rightStep = -leftStep;

            float sway = Mathf.Sin(cycle) * 1.5f;
            float bob  = Mathf.Abs(Mathf.Sin(cycle)) * 0.8f;

            SetOverride(HumanBodyBones.Spine,
                Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f + bob, 0, sway));
            SetOverride(HumanBodyBones.Neck,
                Rest(HumanBodyBones.Neck));
            SetOverride(HumanBodyBones.Head,
                Rest(HumanBodyBones.Head) * Quaternion.Euler(0, 0, sway * 0.2f));

            SetOverride(HumanBodyBones.LeftUpperLeg,
                sLU * Quaternion.Euler(leftStep * legSwing, 0, 0));
            SetOverride(HumanBodyBones.RightUpperLeg,
                sRU * Quaternion.Euler(rightStep * legSwing, 0, 0));

            float leftKnee  = Mathf.Max(0, Mathf.Cos(cycle))  * kneeBend;
            float rightKnee = Mathf.Max(0, -Mathf.Cos(cycle)) * kneeBend;
            SetOverride(HumanBodyBones.LeftLowerLeg,
                sLL * Quaternion.Euler(leftKnee, 0, 0));
            SetOverride(HumanBodyBones.RightLowerLeg,
                sRL * Quaternion.Euler(rightKnee, 0, 0));

            SetOverride(HumanBodyBones.LeftUpperArm,
                aLU * Quaternion.AngleAxis(rightStep * armSwingDeg, swingL));
            SetOverride(HumanBodyBones.RightUpperArm,
                aRU * Quaternion.AngleAxis(leftStep * armSwingDeg, swingR));
            float leftElbow  = Mathf.Max(0, rightStep) * elbowBendDeg;
            float rightElbow = Mathf.Max(0, leftStep)  * elbowBendDeg;
            SetOverride(HumanBodyBones.LeftLowerArm,
                aLL * Quaternion.AngleAxis(leftElbow, swingL));
            SetOverride(HumanBodyBones.RightLowerArm,
                aRL * Quaternion.AngleAxis(rightElbow, swingR));

            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
        _leaveSeatStandingPosition = targetPos;
        _leaveSeatRootLowered = false;

        // ── Phase 4: Settle into standing idle with gentle breathing ──
        var idlePose = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine]         = Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f, 0, 0),
            [HumanBodyBones.Neck]          = Rest(HumanBodyBones.Neck),
            [HumanBodyBones.Head]          = Rest(HumanBodyBones.Head),
            [HumanBodyBones.LeftUpperLeg]  = sLU,
            [HumanBodyBones.RightUpperLeg] = sRU,
            [HumanBodyBones.LeftLowerLeg]  = sLL,
            [HumanBodyBones.RightLowerLeg] = sRL,
            [HumanBodyBones.LeftUpperArm]  = aLU,
            [HumanBodyBones.RightUpperArm] = aRU,
            [HumanBodyBones.LeftLowerArm]  = aLL,
            [HumanBodyBones.RightLowerArm] = aRL,
        };
        yield return StartCoroutine(TransitionTo(idlePose, 0.55f));

        if (layingHoldClip != null)
        {
            if (Mathf.Abs(layingRootYOffset) > 0.001f)
            {
                Vector3 layingRootPosition = _leaveSeatStandingPosition + Vector3.up * layingRootYOffset;
                yield return StartCoroutine(MoveRootToPosition(layingRootPosition, 0.25f));
                _leaveSeatRootLowered = true;
            }

            yield return StartCoroutine(FullBodyClipInCurrentBehavior(
                layingHoldClip,
                "离座躺下",
                -1f,
                0.25f,
                1f,
                true));
            yield break;
        }

        float idleT = 0f;
        while (_behaviorActive)
        {
            float breath = Mathf.Sin(idleT * 1.2f) * 1f;
            SetOverride(HumanBodyBones.Spine,
                Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f + breath * 0.5f, 0, 0));
            idleT += Time.deltaTime;
            yield return null;
        }
    }

    /// RETURN TO SEAT: walk back upright with procedural legs, then sit down.
    IEnumerator ReturnToSeatRoutine(float moveDuration, AnimationClip gettingUpClip)
    {
        CurrentBehaviorName = "回座位";
        while (!_restPoseCaptured) yield return null;
        yield return null;

        if (_leaveSeatRootLowered)
        {
            yield return StartCoroutine(MoveRootToPosition(_leaveSeatStandingPosition, 0.25f));
            _leaveSeatRootLowered = false;
            yield return null;
        }

        if (gettingUpClip != null)
        {
            float getUpDuration = Mathf.Max(0.1f, gettingUpClip.length);
            yield return StartCoroutine(FullBodyClipInCurrentBehavior(
                gettingUpClip,
                "离座起身",
                getUpDuration,
                0.08f,
                1f,
                false));
            yield return null;
        }

        ComputeStandingLegPose(out var sLU, out var sRU, out var sLL, out var sRL);
        ComputeHangingArmPose(out var aLU, out var aRU, out var aLL, out var aRL,
            out var swingL, out var swingR);

        Vector3 targetPos = seatPosition;

        var walkPose = new Dictionary<HumanBodyBones, Quaternion>
        {
            [HumanBodyBones.Spine] = Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f, 0, 0),
            [HumanBodyBones.Neck]  = Rest(HumanBodyBones.Neck),
            [HumanBodyBones.Head]  = Rest(HumanBodyBones.Head),
            [HumanBodyBones.LeftUpperLeg]  = sLU,
            [HumanBodyBones.RightUpperLeg] = sRU,
            [HumanBodyBones.LeftLowerLeg]  = sLL,
            [HumanBodyBones.RightLowerLeg] = sRL,
            [HumanBodyBones.LeftUpperArm]  = aLU,
            [HumanBodyBones.RightUpperArm] = aRU,
            [HumanBodyBones.LeftLowerArm]  = aLL,
            [HumanBodyBones.RightLowerArm] = aRL,
        };
        yield return StartCoroutine(TransitionTo(walkPose, 0.3f));

        // Turn toward seat
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion turnTargetRot = Quaternion.LookRotation(dir.normalized);
            float rotT = 0f;
            Quaternion turnStartRot = transform.rotation;
            while (rotT < 0.25f)
            {
                transform.rotation = Quaternion.Slerp(turnStartRot, turnTargetRot,
                    Mathf.SmoothStep(0, 1, rotT / 0.25f));
                rotT += Time.deltaTime;
                yield return null;
            }
            transform.rotation = turnTargetRot;
        }

        // Walk with procedural legs (upright)
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        moveDuration = Mathf.Max(0.5f, moveDuration);
        const float stepFreq = 4.5f;
        const float legSwing = 20f;
        const float kneeBend = 35f;
        const float armSwingDeg = 20f;
        const float elbowBendDeg = 15f;

        while (elapsed < moveDuration && _behaviorActive)
        {
            float p = Mathf.SmoothStep(0, 1, elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, p);

            float cycle = elapsed * stepFreq;
            float leftStep  = Mathf.Sin(cycle);
            float rightStep = -leftStep;
            float sway = Mathf.Sin(cycle) * 1.5f;

            SetOverride(HumanBodyBones.Spine,
                Rest(HumanBodyBones.Spine) * Quaternion.Euler(2f, 0, sway));
            SetOverride(HumanBodyBones.Head,
                Rest(HumanBodyBones.Head) * Quaternion.Euler(0, 0, sway * 0.2f));

            SetOverride(HumanBodyBones.LeftUpperLeg,
                sLU * Quaternion.Euler(leftStep * legSwing, 0, 0));
            SetOverride(HumanBodyBones.RightUpperLeg,
                sRU * Quaternion.Euler(rightStep * legSwing, 0, 0));

            float leftKnee  = Mathf.Max(0, Mathf.Cos(cycle))  * kneeBend;
            float rightKnee = Mathf.Max(0, -Mathf.Cos(cycle)) * kneeBend;
            SetOverride(HumanBodyBones.LeftLowerLeg,
                sLL * Quaternion.Euler(leftKnee, 0, 0));
            SetOverride(HumanBodyBones.RightLowerLeg,
                sRL * Quaternion.Euler(rightKnee, 0, 0));

            SetOverride(HumanBodyBones.LeftUpperArm,
                aLU * Quaternion.AngleAxis(rightStep * armSwingDeg, swingL));
            SetOverride(HumanBodyBones.RightUpperArm,
                aRU * Quaternion.AngleAxis(leftStep * armSwingDeg, swingR));
            float leftElbow  = Mathf.Max(0, rightStep) * elbowBendDeg;
            float rightElbow = Mathf.Max(0, leftStep)  * elbowBendDeg;
            SetOverride(HumanBodyBones.LeftLowerArm,
                aLL * Quaternion.AngleAxis(leftElbow, swingL));
            SetOverride(HumanBodyBones.RightLowerArm,
                aRL * Quaternion.AngleAxis(rightElbow, swingR));

            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        // Restore original seated facing direction before releasing the bone overrides.
        // Without this, a student who leaves sideways returns to the seat still facing sideways.
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = seatRotation;
        if (Quaternion.Angle(startRot, targetRot) > 0.5f)
        {
            float rotT = 0f;
            const float rotateBackSeconds = 0.45f;
            while (rotT < rotateBackSeconds && _behaviorActive)
            {
                transform.rotation = Quaternion.Slerp(
                    startRot,
                    targetRot,
                    Mathf.SmoothStep(0f, 1f, rotT / rotateBackSeconds));
                rotT += Time.deltaTime;
                yield return null;
            }
        }

        transform.rotation = targetRot;
    }
}
