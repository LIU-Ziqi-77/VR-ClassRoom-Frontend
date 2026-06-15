using UnityEngine;

/// <summary>
/// Adds subtle head, neck, and upper-body orientation toward the active gaze target.
/// Eye motion remains handled by VRM LookAt; this script layers a small local pose
/// offset after the Animator so the child appears to orient realistically.
/// </summary>
public class DTTUpperBodyGazeFollower : MonoBehaviour
{
    [Header("References")]
    public Animator humanoidAnimator;
    public EyeController eyeController;
    public Transform targetOverride;

    [Header("Enable")]
    public bool followEnabled = true;
    public bool useEyeControllerTarget = true;

    [Header("Distribution")]
    [Range(0f, 1f)]
    public float headYawWeight = 0.45f;
    [Range(0f, 1f)]
    public float neckYawWeight = 0.25f;
    [Range(0f, 1f)]
    public float chestYawWeight = 0.14f;
    [Range(0f, 1f)]
    public float spineYawWeight = 0.06f;

    [Range(0f, 1f)]
    public float headPitchWeight = 0.36f;
    [Range(0f, 1f)]
    public float neckPitchWeight = 0.2f;
    [Range(0f, 1f)]
    public float chestPitchWeight = 0.08f;
    [Range(0f, 1f)]
    public float spinePitchWeight = 0.03f;

    [Header("Limits")]
    public float maxHeadYaw = 24f;
    public float maxNeckYaw = 16f;
    public float maxChestYaw = 10f;
    public float maxSpineYaw = 5f;
    public float maxHeadPitch = 14f;
    public float maxNeckPitch = 8f;
    public float maxChestPitch = 5f;
    public float maxSpinePitch = 3f;

    [Header("Motion")]
    public float followSmoothTime = 0.32f;
    public float returnSmoothTime = 0.45f;
    public float deadZoneYaw = 4f;
    public float deadZonePitch = 3f;
    public float maxSourceYaw = 70f;
    public float maxSourcePitch = 35f;

    private Transform headBone;
    private Transform neckBone;
    private Transform chestBone;
    private Transform spineBone;

    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;
    private float pitchVelocity;

    void Awake()
    {
        Initialize();
    }

    void LateUpdate()
    {
        if (!followEnabled)
        {
            SmoothToNeutral();
            ApplyPoseOffsets();
            return;
        }

        Vector3 targetPosition;
        bool hasTarget = TryGetTargetPosition(out targetPosition);
        if (!hasTarget)
        {
            SmoothToNeutral();
            ApplyPoseOffsets();
            return;
        }

        Vector3 localDirection = transform.InverseTransformDirection(targetPosition - GetReferencePosition());
        if (localDirection.sqrMagnitude < 0.0001f)
        {
            SmoothToNeutral();
            ApplyPoseOffsets();
            return;
        }

        localDirection.Normalize();
        float targetYaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;

        targetYaw = ApplyDeadZone(Mathf.Clamp(targetYaw, -maxSourceYaw, maxSourceYaw), deadZoneYaw);
        targetPitch = ApplyDeadZone(Mathf.Clamp(targetPitch, -maxSourcePitch, maxSourcePitch), deadZonePitch);

        float smoothTime = Mathf.Max(0.01f, followSmoothTime);
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, smoothTime);
        currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, smoothTime);

        ApplyPoseOffsets();

        if (eyeController != null)
        {
            eyeController.RefreshLookAtNow();
        }
    }

    private void Initialize()
    {
        if (humanoidAnimator == null)
        {
            humanoidAnimator = GetComponent<Animator>();
        }

        if (eyeController == null)
        {
            eyeController = GetComponent<EyeController>();
        }

        if (humanoidAnimator != null && humanoidAnimator.isHuman)
        {
            headBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.Head);
            neckBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.Neck);
            chestBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chestBone == null)
            {
                chestBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }
            spineBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.Spine);
        }
    }

    private bool TryGetTargetPosition(out Vector3 targetPosition)
    {
        if (targetOverride != null)
        {
            targetPosition = targetOverride.position;
            return true;
        }

        if (useEyeControllerTarget && eyeController != null)
        {
            if (eyeController.useTargetTransform && eyeController.currentTarget != null)
            {
                targetPosition = eyeController.currentTarget.position;
                return true;
            }

            if (!eyeController.useTargetTransform)
            {
                targetPosition = eyeController.targetPosition;
                return true;
            }
        }

        targetPosition = Vector3.zero;
        return false;
    }

    private Vector3 GetReferencePosition()
    {
        if (headBone != null)
        {
            return headBone.position;
        }

        return transform.position + Vector3.up * 1.2f;
    }

    private float ApplyDeadZone(float value, float deadZone)
    {
        float abs = Mathf.Abs(value);
        if (abs <= deadZone)
        {
            return 0f;
        }

        return Mathf.Sign(value) * (abs - deadZone);
    }

    private void SmoothToNeutral()
    {
        float smoothTime = Mathf.Max(0.01f, returnSmoothTime);
        currentYaw = Mathf.SmoothDampAngle(currentYaw, 0f, ref yawVelocity, smoothTime);
        currentPitch = Mathf.SmoothDampAngle(currentPitch, 0f, ref pitchVelocity, smoothTime);
    }

    private void ApplyPoseOffsets()
    {
        ApplyBoneOffset(spineBone, spineYawWeight, spinePitchWeight, maxSpineYaw, maxSpinePitch);
        ApplyBoneOffset(chestBone, chestYawWeight, chestPitchWeight, maxChestYaw, maxChestPitch);
        ApplyBoneOffset(neckBone, neckYawWeight, neckPitchWeight, maxNeckYaw, maxNeckPitch);
        ApplyBoneOffset(headBone, headYawWeight, headPitchWeight, maxHeadYaw, maxHeadPitch);
    }

    private void ApplyBoneOffset(Transform bone, float yawWeight, float pitchWeight, float maxYaw, float maxPitch)
    {
        if (bone == null) return;

        float yaw = Mathf.Clamp(currentYaw * yawWeight, -maxYaw, maxYaw);
        float pitch = Mathf.Clamp(currentPitch * pitchWeight, -maxPitch, maxPitch);

        Quaternion offset = Quaternion.Euler(pitch, yaw, 0f);
        bone.localRotation = bone.localRotation * offset;
    }
}
