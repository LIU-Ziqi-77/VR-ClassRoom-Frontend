using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRM;

public enum StudentBehaviorType
{
    Idle,
    Speaking,
    Listening,
    RaisingHand,
    TakingNotes,
    LookingAround,
    Confused,
    Excited,
    OffTask,
    LookAtBoard,
    LeaveSeat,
    SelfTalk,
    OffTopicToTeacher,
    GazeAtTeacher,
    TouchTeacher,
    TakeDeskItem,
    Scream,
    HitDesk,
    PushPeer,
    LieDown,
    SelfHit
}

[System.Serializable]
public class StudentCommand
{
    public string studentId;
    public CommandType type;
    public string text;
    public Vector3 targetPosition;
    public StudentBehaviorType behaviorType;
    public string targetStudentId;
    public float duration;
}

public enum CommandType
{
    Speak,
    LookAt,
    Behavior,
    Gesture,
    Stop
}

public class StudentBehaviorController : MonoBehaviour
{
    [Header("Student Identity")]
    public string studentId;
    public string studentName;
    
    [Header("VRM Components")]
    public VRMBlendShapeProxy blendShapeProxy;
    public Animator animator;
    
    [Header("Controllers")]
    public LipSyncController lipSyncController;
    public EyeController eyeController;
    
    [Header("Voice Configuration")]
    public StudentVoiceConfig voiceConfig;
    
    [Header("Behavior Settings")]
    public float attentionSpan = 10f;
    public float speakingConfidence = 0.8f;
    
    [Header("Animation States")]
    public string idleAnimationName = "Idle";
    public string speakingAnimationName = "Speaking";
    public string listeningAnimationName = "Listening";
    public string raisingHandAnimationName = "RaisingHand";
    public string lookAtBoardAnimationName = "LookAtBoard";
    public string leaveSeatAnimationName = "LeaveSeat";
    public string selfTalkAnimationName = "SelfTalk";
    public string offTopicAnimationName = "OffTopic";
    public string touchTeacherAnimationName = "TouchTeacher";
    public string takeDeskItemAnimationName = "TakeDeskItem";
    public string lieDownAnimationName = "LieDown";
    
    [Header("Animation Triggers")]
    public string screamTriggerName = "Scream";
    public string hitDeskTriggerName = "HitDesk";
    public string pushPeerTriggerName = "PushPeer";
    public string selfHitTriggerName = "SelfHit";
    
    private StudentBehaviorType currentBehavior = StudentBehaviorType.Idle;
    private bool isSpeaking = false;
    private bool isListening = false;
    private Coroutine currentBehaviorCoroutine;
    
    void Start()
    {
        InitializeComponents();
        StartIdleBehavior();
    }
    
    private void InitializeComponents()
    {
        if (blendShapeProxy == null)
            blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (lipSyncController == null)
            lipSyncController = GetComponent<LipSyncController>();
        
        if (eyeController == null)
            eyeController = GetComponent<EyeController>();
        
        if (lipSyncController != null)
            lipSyncController.blendShapeProxy = blendShapeProxy;
        
        if (eyeController != null)
        {
            eyeController.humanoidAnimator = animator;
            eyeController.blendShapeProxy = blendShapeProxy;
        }
    }
    
    public void ProcessCommand(StudentCommand command)
    {
        if (command.studentId != studentId) return;
        
        switch (command.type)
        {
            case CommandType.Speak:
                _ = SpeakWithLipSync(command.text);
                break;
                
            case CommandType.LookAt:
                if (!string.IsNullOrEmpty(command.targetStudentId))
                {
                    eyeController.LookAtStudent(command.targetStudentId);
                }
                else
                {
                    eyeController.LookAtPosition(command.targetPosition);
                }
                break;
                
            case CommandType.Behavior:
                SetBehavior(command.behaviorType, command.duration);
                break;
                
            case CommandType.Gesture:
                PlayGesture(command.behaviorType);
                break;
                
            case CommandType.Stop:
                StopCurrentBehavior();
                break;
        }
    }
    
    public async Task SpeakWithLipSync(string text)
    {
        if (isSpeaking) return;
        
        isSpeaking = true;
        SetBehavior(StudentBehaviorType.Speaking);
        
        try
        {
            AudioClip speechClip = null;
            
            // 使用个性化声音配置
            if (voiceConfig != null)
            {
                string ssml = voiceConfig.GetSSMLWithPersonality(text);
                speechClip = await TTSService.Instance.GenerateSpeechWithSSML(ssml);
                Debug.Log($"[{studentName}] Using voice: {voiceConfig.GetSelectedVoiceConfig().displayName}");
            }
            else
            {
                // 使用默认声音
                speechClip = await TTSService.Instance.GenerateSpeech(text);
            }
            
            if (speechClip != null)
            {
                // 分析唇形同步数据
                LipSyncData lipSyncData = await lipSyncController.AnalyzeLipSync(speechClip);
                
                // 播放音频和唇形同步
                StartCoroutine(lipSyncController.PlayWithLipSync(speechClip, lipSyncData));
                
                // 等待播放完成
                await Task.Delay((int)(speechClip.length * 1000));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Speech generation failed: {e.Message}");
        }
        finally
        {
            isSpeaking = false;
            SetBehavior(StudentBehaviorType.Idle);
        }
    }
    
    public void SetBehavior(StudentBehaviorType behavior, float duration = 0f)
    {
        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
        }
        
        currentBehavior = behavior;
        
        switch (behavior)
        {
            case StudentBehaviorType.Idle:
                PlayIdleAnimation();
                break;
                
            case StudentBehaviorType.Speaking:
                PlaySpeakingAnimation();
                break;
                
            case StudentBehaviorType.Listening:
                PlayListeningAnimation();
                break;
                
            case StudentBehaviorType.RaisingHand:
                PlayRaisingHandAnimation();
                break;
                
            case StudentBehaviorType.TakingNotes:
                PlayTakingNotesAnimation();
                break;
                
            case StudentBehaviorType.LookingAround:
                StartCoroutine(LookAroundBehavior());
                break;
                
            case StudentBehaviorType.Confused:
                PlayConfusedExpression();
                break;
                
            case StudentBehaviorType.Excited:
                PlayExcitedExpression();
                break;

            case StudentBehaviorType.OffTask:
                PlayIdleAnimation();
                break;

            case StudentBehaviorType.LookAtBoard:
                PlaySimpleAnimation(lookAtBoardAnimationName);
                if (eyeController != null)
                {
                    eyeController.LookAtPosition(transform.position + transform.forward * 5f + Vector3.up * 1.5f);
                }
                break;

            case StudentBehaviorType.LeaveSeat:
                PlaySimpleAnimation(leaveSeatAnimationName);
                break;

            case StudentBehaviorType.SelfTalk:
                PlaySimpleAnimation(selfTalkAnimationName);
                break;

            case StudentBehaviorType.OffTopicToTeacher:
                PlaySimpleAnimation(offTopicAnimationName);
                if (eyeController != null)
                {
                    eyeController.LookAtTeacher();
                }
                break;

            case StudentBehaviorType.GazeAtTeacher:
                PlayListeningAnimation();
                break;

            case StudentBehaviorType.TouchTeacher:
                PlaySimpleAnimation(touchTeacherAnimationName);
                break;

            case StudentBehaviorType.TakeDeskItem:
                PlaySimpleAnimation(takeDeskItemAnimationName);
                break;

            case StudentBehaviorType.Scream:
                TriggerIfSet(screamTriggerName);
                break;

            case StudentBehaviorType.HitDesk:
                TriggerIfSet(hitDeskTriggerName);
                break;

            case StudentBehaviorType.PushPeer:
                TriggerIfSet(pushPeerTriggerName);
                break;

            case StudentBehaviorType.LieDown:
                PlaySimpleAnimation(lieDownAnimationName);
                break;

            case StudentBehaviorType.SelfHit:
                TriggerIfSet(selfHitTriggerName);
                break;
        }
        
        if (duration > 0)
        {
            currentBehaviorCoroutine = StartCoroutine(ResetBehaviorAfterDuration(duration));
        }
    }
    
    public void PlayGesture(StudentBehaviorType gestureType)
    {
        switch (gestureType)
        {
            case StudentBehaviorType.RaisingHand:
                PlayRaisingHandAnimation();
                break;
                
            case StudentBehaviorType.TakingNotes:
                PlayTakingNotesAnimation();
                break;
        }
    }
    
    public void StopCurrentBehavior()
    {
        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
        }
        
        if (isSpeaking)
        {
            lipSyncController.StopLipSync();
            isSpeaking = false;
        }
        
        SetBehavior(StudentBehaviorType.Idle);
    }
    
    private void StartIdleBehavior()
    {
        SetBehavior(StudentBehaviorType.Idle);
    }
    
    private void PlayIdleAnimation()
    {
        if (animator != null)
        {
            animator.Play(idleAnimationName);
        }
    }
    
    private void PlaySpeakingAnimation()
    {
        if (animator != null)
        {
            animator.Play(speakingAnimationName);
        }
    }
    
    private void PlayListeningAnimation()
    {
        if (animator != null)
        {
            animator.Play(listeningAnimationName);
        }
        
        // 看向教师
        eyeController.LookAtTeacher();
    }
    
    private void PlayRaisingHandAnimation()
    {
        if (animator != null)
        {
            animator.Play(raisingHandAnimationName);
        }
    }
    
    private void PlayTakingNotesAnimation()
    {
        // 实现做笔记的动画
        if (animator != null)
        {
            animator.SetTrigger("TakeNotes");
        }
    }

    private void PlaySimpleAnimation(string animationName)
    {
        if (animator != null && !string.IsNullOrEmpty(animationName))
        {
            animator.Play(animationName);
        }
    }

    private void TriggerIfSet(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
    
    private IEnumerator LookAroundBehavior()
    {
        while (currentBehavior == StudentBehaviorType.LookingAround)
        {
            // 随机看向不同方向
            Vector3 randomDirection = Random.onUnitSphere;
            Vector3 lookTarget = transform.position + randomDirection * 5f;
            eyeController.LookAtPosition(lookTarget);
            
            yield return new WaitForSeconds(Random.Range(2f, 5f));
        }
    }
    
    private void PlayConfusedExpression()
    {
        if (blendShapeProxy != null)
        {
            blendShapeProxy.SetValue("Confused", 1f);
        }
    }
    
    private void PlayExcitedExpression()
    {
        if (blendShapeProxy != null)
        {
            blendShapeProxy.SetValue("Happy", 1f);
        }
    }
    
    private IEnumerator ResetBehaviorAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        SetBehavior(StudentBehaviorType.Idle);
    }
    
    public void LookAtTeacher()
    {
        eyeController.LookAtTeacher();
    }
    
    public void LookAtStudent(string targetStudentId)
    {
        eyeController.LookAtStudent(targetStudentId);
    }
    
    public void LookAtPosition(Vector3 position)
    {
        eyeController.LookAtPosition(position);
    }
    
    public void ResetEyeDirection()
    {
        eyeController.ResetEyeDirection();
    }
    
    public bool IsSpeaking()
    {
        return isSpeaking;
    }
    
    public StudentBehaviorType GetCurrentBehavior()
    {
        return currentBehavior;
    }
    
    public string GetStudentId()
    {
        return studentId;
    }
    
    public string GetStudentName()
    {
        return studentName;
    }
} 