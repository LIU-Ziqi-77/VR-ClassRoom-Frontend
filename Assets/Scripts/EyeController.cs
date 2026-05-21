using UnityEngine;
using System.Collections;
using VRM;

public class EyeController : MonoBehaviour
{
    [Header("VRM Components")]
    public Animator humanoidAnimator;
    public VRMBlendShapeProxy blendShapeProxy;
    public VRMLookAtHead vrmLookAtHead;
    
    [Header("Eye Bones")]
    public Transform leftEyeBone;
    public Transform rightEyeBone;
    
    [Header("Eye Control Settings")]
    public float lookSpeed = 5f;
    public float maxLookAngle = 30f;
    public float blinkInterval = 3f;
    public float blinkDuration = 0.1f;

    [Header("Natural Gaze Settings")]
    public bool enableNaturalGaze = true;
    [Tooltip("Adds small target offsets so the character does not stare at one exact point.")]
    public float targetJitterRadius = 0.08f;
    [Tooltip("How often the eyes pick a slightly different point near the current target.")]
    public Vector2 microSaccadeIntervalRange = new Vector2(0.8f, 2.2f);
    [Tooltip("Randomized blink interval range. Overrides the fixed Blink Interval when enabled.")]
    public Vector2 blinkIntervalRange = new Vector2(2.2f, 6f);
    [Header("BlendShape Names")]
    public string lookUpBlendShapeName = "LookUp";
    public string lookDownBlendShapeName = "LookDown";
    public string lookLeftBlendShapeName = "LookLeft";
    public string lookRightBlendShapeName = "LookRight";
    public string blinkBlendShapeName = "Blink";
    
    [Header("Target Settings")]
    public Transform currentTarget;
    public Vector3 targetPosition;
    public bool useTargetTransform = true;
    
    private Vector3 originalLeftEyeRotation;
    private Vector3 originalRightEyeRotation;
    private bool isBlinking = false;
    private float lastBlinkTime;
    private float nextBlinkTime;
    private float nextMicroSaccadeTime;
    private Vector3 currentTargetOffset;
    private Transform vrmLookAtRuntimeTarget;
    
    void Start()
    {
        InitializeEyeController();
    }
    
    void Update()
    {
        HandleBlinking();
        UpdateEyeDirection();
    }

    private void InitializeEyeController()
    {
        if (humanoidAnimator == null)
        {
            humanoidAnimator = GetComponent<Animator>();
        }
        
        if (blendShapeProxy == null)
        {
            blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
        }

        if (vrmLookAtHead == null)
        {
            vrmLookAtHead = GetComponent<VRMLookAtHead>();
        }
        
        if (leftEyeBone == null && humanoidAnimator != null && humanoidAnimator.isHuman)
        {
            leftEyeBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.LeftEye);
        }
        
        if (rightEyeBone == null && humanoidAnimator != null && humanoidAnimator.isHuman)
        {
            rightEyeBone = humanoidAnimator.GetBoneTransform(HumanBodyBones.RightEye);
        }

        // 保存原始眼球旋转
        if (leftEyeBone != null)
        {
            originalLeftEyeRotation = leftEyeBone.localEulerAngles;
        }
        
        if (rightEyeBone != null)
        {
            originalRightEyeRotation = rightEyeBone.localEulerAngles;
        }

        lastBlinkTime = Time.time;
        ScheduleNextBlink();
        ScheduleNextMicroSaccade();
    }
    
    private void HandleBlinking()
    {
        if (isBlinking) return;
        
        if (Time.time >= nextBlinkTime)
        {
            StartCoroutine(Blink());
        }
    }
    
    private IEnumerator Blink()
    {
        isBlinking = true;
        
        // 眨眼动画
        if (blendShapeProxy != null)
        {
            // 眨眼开始
            blendShapeProxy.SetValue(blinkBlendShapeName, 1f);
            yield return new WaitForSeconds(blinkDuration * 0.5f);
            
            // 眨眼结束
            blendShapeProxy.SetValue(blinkBlendShapeName, 0f);
            yield return new WaitForSeconds(blinkDuration * 0.5f);
        }
        
        isBlinking = false;
        lastBlinkTime = Time.time;
        ScheduleNextBlink();
    }
    
    private void UpdateEyeDirection()
    {
        if (useTargetTransform && currentTarget == null) return;
        
        Vector3 targetPos = useTargetTransform ? currentTarget.position : targetPosition;
        ApplyLookAtTarget(targetPos);
    }
    
    public void LookAtTarget(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
        useTargetTransform = false;

        ApplyLookAtTarget(targetPosition);
    }

    private void ApplyLookAtTarget(Vector3 targetPosition)
    {
        Vector3 adjustedTarget = GetNaturalTargetPosition(targetPosition);

        bool usedVrmLookAt = ApplyVrmLookAt(adjustedTarget);

        if (!usedVrmLookAt && leftEyeBone != null)
        {
            Vector3 leftEyeDirection = CalculateEyeDirection(leftEyeBone.position, adjustedTarget);
            ApplyEyeRotation(leftEyeBone, leftEyeDirection);
        }

        if (!usedVrmLookAt && rightEyeBone != null)
        {
            Vector3 rightEyeDirection = CalculateEyeDirection(rightEyeBone.position, adjustedTarget);
            ApplyEyeRotation(rightEyeBone, rightEyeDirection);
        }

        if (!usedVrmLookAt)
        {
            // 同时更新BlendShape（如果可用）
            UpdateBlendShapeLookDirection(adjustedTarget);
        }
    }
    
    public void LookAtTransform(Transform target)
    {
        currentTarget = target;
        useTargetTransform = true;
    }
    
    public void LookAtTeacher()
    {
        // 查找教师位置（假设教师是场景中的特定对象）
        Transform teacher = FindTeacherInScene();
        if (teacher != null)
        {
            LookAtTransform(teacher);
        }
    }
    
    public void LookAtStudent(string studentId)
    {
        // 查找指定学生
        Transform student = FindStudentInScene(studentId);
        if (student != null)
        {
            LookAtTransform(student);
        }
    }
    
    public void LookAtPosition(Vector3 position)
    {
        LookAtTarget(position);
    }

    public void RefreshLookAtNow()
    {
        UpdateEyeDirection();
    }
    
    public void ResetEyeDirection()
    {
        if (leftEyeBone != null)
        {
            leftEyeBone.localEulerAngles = originalLeftEyeRotation;
        }
        
        if (rightEyeBone != null)
        {
            rightEyeBone.localEulerAngles = originalRightEyeRotation;
        }

        // 重置BlendShape
        if (blendShapeProxy != null)
        {
            blendShapeProxy.SetValue(lookUpBlendShapeName, 0f);
            blendShapeProxy.SetValue(lookDownBlendShapeName, 0f);
            blendShapeProxy.SetValue(lookLeftBlendShapeName, 0f);
            blendShapeProxy.SetValue(lookRightBlendShapeName, 0f);
        }
        
        currentTarget = null;
        useTargetTransform = true;
    }
    
    private Vector3 CalculateEyeDirection(Vector3 eyePosition, Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - eyePosition).normalized;
        
        // 限制眼球旋转角度
        float angle = Vector3.Angle(transform.forward, direction);
        if (angle > maxLookAngle)
        {
            direction = Vector3.RotateTowards(transform.forward, direction, 
                maxLookAngle * Mathf.Deg2Rad, 0f);
        }
        
        return direction;
    }

    private Vector3 GetNaturalTargetPosition(Vector3 baseTargetPosition)
    {
        if (!enableNaturalGaze || targetJitterRadius <= 0f)
        {
            return baseTargetPosition;
        }

        if (Time.time >= nextMicroSaccadeTime)
        {
            currentTargetOffset = Random.insideUnitSphere * targetJitterRadius;
            currentTargetOffset.y *= 0.65f;
            ScheduleNextMicroSaccade();
        }

        return baseTargetPosition + currentTargetOffset;
    }

    private void ScheduleNextMicroSaccade()
    {
        float min = Mathf.Max(0.05f, microSaccadeIntervalRange.x);
        float max = Mathf.Max(min, microSaccadeIntervalRange.y);
        nextMicroSaccadeTime = Time.time + Random.Range(min, max);
    }

    private void ScheduleNextBlink()
    {
        if (enableNaturalGaze)
        {
            float min = Mathf.Max(0.2f, blinkIntervalRange.x);
            float max = Mathf.Max(min, blinkIntervalRange.y);
            nextBlinkTime = Time.time + Random.Range(min, max);
        }
        else
        {
            nextBlinkTime = Time.time + Mathf.Max(0.2f, blinkInterval);
        }
    }
    
    private void ApplyEyeRotation(Transform eyeBone, Vector3 direction)
    {
        if (eyeBone == null) return;
        
        // 计算目标旋转
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        
        // 平滑旋转
        eyeBone.rotation = Quaternion.Slerp(eyeBone.rotation, targetRotation, 
            lookSpeed * Time.deltaTime);
    }

    private bool ApplyVrmLookAt(Vector3 targetPosition)
    {
        if (vrmLookAtHead == null || vrmLookAtHead.Head == null)
        {
            return false;
        }

        EnsureVrmLookAtRuntimeTarget();
        vrmLookAtRuntimeTarget.position = targetPosition;
        vrmLookAtHead.Target = vrmLookAtRuntimeTarget;

        float yaw;
        float pitch;
        vrmLookAtHead.LookWorldPosition(targetPosition, out yaw, out pitch);
        return true;
    }

    private void EnsureVrmLookAtRuntimeTarget()
    {
        if (vrmLookAtRuntimeTarget != null) return;

        GameObject target = new GameObject($"{name}_DTTLookAtTarget");
        target.hideFlags = HideFlags.HideInHierarchy;
        vrmLookAtRuntimeTarget = target.transform;
    }
    
    private void UpdateBlendShapeLookDirection(Vector3 targetPosition)
    {
        if (blendShapeProxy == null) return;
        
        // 计算相对于头部的方向
        Vector3 localDirection = transform.InverseTransformDirection(targetPosition - transform.position);
        
        // 计算上下左右的角度
        float upDownAngle = Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;
        float leftRightAngle = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        
        // 映射到BlendShape值
        float upValue = Mathf.Clamp01(upDownAngle / maxLookAngle);
        float downValue = Mathf.Clamp01(-upDownAngle / maxLookAngle);
        float leftValue = Mathf.Clamp01(-leftRightAngle / maxLookAngle);
        float rightValue = Mathf.Clamp01(leftRightAngle / maxLookAngle);
        
        // 设置BlendShape值
        blendShapeProxy.SetValue(lookUpBlendShapeName, upValue);
        blendShapeProxy.SetValue(lookDownBlendShapeName, downValue);
        blendShapeProxy.SetValue(lookLeftBlendShapeName, leftValue);
        blendShapeProxy.SetValue(lookRightBlendShapeName, rightValue);
    }
    
    private Transform FindTeacherInScene()
    {
        // 查找标记为教师的对象
        GameObject teacher = GameObject.FindGameObjectWithTag("Teacher");
        if (teacher != null)
        {
            return teacher.transform;
        }
        
        // 或者查找特定名称的对象
        teacher = GameObject.Find("Teacher");
        if (teacher != null)
        {
            return teacher.transform;
        }
        
        return null;
    }
    
    private Transform FindStudentInScene(string studentId)
    {
        // 查找指定ID的学生
        GameObject student = GameObject.Find($"Student_{studentId}");
        if (student != null)
        {
            return student.transform;
        }
        
        return null;
    }
    
    public void SetBlinkInterval(float interval)
    {
        blinkInterval = interval;
        blinkIntervalRange = new Vector2(interval, interval);
        ScheduleNextBlink();
    }
    
    public void SetLookSpeed(float speed)
    {
        lookSpeed = speed;
    }
    
    public void SetMaxLookAngle(float angle)
    {
        maxLookAngle = angle;
    }
} 
