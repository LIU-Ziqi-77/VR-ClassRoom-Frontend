using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RandomBehaviorOption
{
    public StudentBehaviorType behavior = StudentBehaviorType.OffTask;
    [Tooltip("权重越高被选中的概率越大")]
    public float weight = 1f;
    [Tooltip("行为持续时间范围")]
    public Vector2 durationRange = new Vector2(2f, 4f);
}

/// <summary>
/// 为学生注入不被提问时的随机/干扰行为。
/// 将本组件挂在学生对象上，并配置权重与目标引用。
/// </summary>
public class StudentRandomBehavior : MonoBehaviour
{
    [Header("References")]
    public StudentBehaviorController controller;
    public Transform boardLookTarget;
    public Transform teacherTarget;

    [Header("Timing")]
    public Vector2 intervalRange = new Vector2(3f, 8f);

    [Header("可选行为列表")]
    public List<RandomBehaviorOption> randomBehaviors = new List<RandomBehaviorOption>
    {
        new RandomBehaviorOption { behavior = StudentBehaviorType.LookAtBoard, weight = 1f, durationRange = new Vector2(2f, 4f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.SelfTalk, weight = 0.8f, durationRange = new Vector2(2f, 4f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.OffTopicToTeacher, weight = 0.6f, durationRange = new Vector2(3f, 5f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.TakeDeskItem, weight = 0.6f, durationRange = new Vector2(2f, 3.5f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.HitDesk, weight = 0.4f, durationRange = new Vector2(1.5f, 2.5f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.Scream, weight = 0.3f, durationRange = new Vector2(1f, 2f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.LieDown, weight = 0.2f, durationRange = new Vector2(3f, 5f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.PushPeer, weight = 0.2f, durationRange = new Vector2(1.5f, 3f)},
        new RandomBehaviorOption { behavior = StudentBehaviorType.SelfHit, weight = 0.2f, durationRange = new Vector2(1.5f, 2.5f)}
    };

    private Coroutine loopRoutine;

    void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<StudentBehaviorController>();
        }
    }

    void OnEnable()
    {
        loopRoutine = StartCoroutine(RunLoop());
    }

    void OnDisable()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
        }
    }

    private IEnumerator RunLoop()
    {
        while (true)
        {
            float wait = Random.Range(intervalRange.x, intervalRange.y);
            yield return new WaitForSeconds(wait);

            if (controller == null) continue;

            // 避免打断正在说话的学生
            if (controller.IsSpeaking()) continue;

            RandomBehaviorOption option = PickBehavior();
            if (option == null) continue;

            ApplyLookTargets(option.behavior);
            float duration = Random.Range(option.durationRange.x, option.durationRange.y);
            controller.SetBehavior(option.behavior, duration);
        }
    }

    private RandomBehaviorOption PickBehavior()
    {
        if (randomBehaviors == null || randomBehaviors.Count == 0) return null;

        float total = 0f;
        foreach (var b in randomBehaviors)
        {
            total += Mathf.Max(0f, b.weight);
        }
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float accum = 0f;
        foreach (var b in randomBehaviors)
        {
            accum += Mathf.Max(0f, b.weight);
            if (roll <= accum)
            {
                return b;
            }
        }

        return randomBehaviors[randomBehaviors.Count - 1];
    }

    private void ApplyLookTargets(StudentBehaviorType behavior)
    {
        if (controller == null || controller.eyeController == null) return;

        switch (behavior)
        {
            case StudentBehaviorType.LookAtBoard:
                if (boardLookTarget != null)
                {
                    controller.eyeController.LookAtTransform(boardLookTarget);
                }
                break;
            case StudentBehaviorType.GazeAtTeacher:
            case StudentBehaviorType.OffTopicToTeacher:
                if (teacherTarget != null)
                {
                    controller.eyeController.LookAtTransform(teacherTarget);
                }
                else
                {
                    controller.eyeController.LookAtTeacher();
                }
                break;
        }
    }
}

