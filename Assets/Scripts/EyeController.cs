using UnityEngine;
using System.Collections;
using VRM;

public class EyeController : MonoBehaviour
{
    [Header("VRM Components")]
    public Animator humanoidAnimator;
    public VRMBlendShapeProxy blendShapeProxy;
    
    [Header("Eye Bones")]
    public Transform leftEyeBone;
    public Transform rightEyeBone;
    
    [Header("Eye Control Settings")]
    public float lookSpeed = 5f;
    public float maxLookAngle = 30f;
    public float blinkInterval = 3f;
    public float blinkDuration = 0.1f;
    
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
    }
    
    private void HandleBlinking()
    {
        if (isBlinking) return;
        
        if (Time.time - lastBlinkTime > blinkInterval)
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
    }
    
    private void UpdateEyeDirection()
    {
        if (currentTarget == null && !useTargetTransform) return;
        
        Vector3 targetPos = useTargetTransform ? currentTarget.position : targetPosition;
        LookAtTarget(targetPos);
    }
    
    public void LookAtTarget(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
        useTargetTransform = false;
        
        if (leftEyeBone == null || rightEyeBone == null) return;
        
        // 计算眼球旋转
        Vector3 leftEyeDirection = CalculateEyeDirection(leftEyeBone.position, targetPosition);
        Vector3 rightEyeDirection = CalculateEyeDirection(rightEyeBone.position, targetPosition);
        
        // 应用眼球旋转
        ApplyEyeRotation(leftEyeBone, leftEyeDirection);
        ApplyEyeRotation(rightEyeBone, rightEyeDirection);
        
        // 同时更新BlendShape（如果可用）
        UpdateBlendShapeLookDirection(targetPosition);
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
        useTargetTransform = false;
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
    
    private void ApplyEyeRotation(Transform eyeBone, Vector3 direction)
    {
        if (eyeBone == null) return;
        
        // 计算目标旋转
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        
        // 平滑旋转
        eyeBone.rotation = Quaternion.Slerp(eyeBone.rotation, targetRotation, 
            lookSpeed * Time.deltaTime);
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