using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum ClassroomItemType
{
    Cup,
    Pencil,
    Eraser,
    Ruler
}

public enum StudentResponseKind
{
    NoResponse,
    Incorrect,
    Correct
}

[System.Serializable]
public class ClassroomItemDefinition
{
    public ClassroomItemType itemType;
    public string displayName;
    [Tooltip("回答正确时可用的词汇/短语")]
    public List<string> correctAnswers = new List<string>();
    [Tooltip("回答错误时会使用的词汇/短语")]
    public List<string> wrongAnswers = new List<string>();
}

/// <summary>
/// 课堂情境控制：教师拿起物品并提问，学生按概率做出反应。
/// </summary>
public class ClassroomScenarioController : MonoBehaviour
{
    [Header("学生与教师引用")]
    public List<StudentBehaviorController> students = new List<StudentBehaviorController>();
    public Transform teacherTransform;

    [Header("问答配置")]
    [Range(0f, 1f)] public float noResponseProbability = 0.2f;
    [Range(0f, 1f)] public float correctResponseProbability = 0.5f;
    public float postAnswerListenDuration = 2f;
    public List<ClassroomItemDefinition> itemDefinitions = new List<ClassroomItemDefinition>();
    public List<string> fallbackWrongAnswers = new List<string>
    {
        "不知道",
        "这是别的东西吧",
        "我在想别的事情",
        "嗯？"
    };

    [Header("状态追踪")]
    public ClassroomItemType currentItem = ClassroomItemType.Cup;
    public StudentBehaviorController currentTargetStudent;

    void Awake()
    {
        if (students.Count == 0)
        {
            students.AddRange(FindObjectsOfType<StudentBehaviorController>());
        }
    }

    /// <summary>从外部（抓取事件或UI）设置当前拿着的物品。</summary>
    public void SetCurrentItem(ClassroomItemType itemType)
    {
        currentItem = itemType;
    }

    /// <summary>指定被提问的学生（可由射线/指向事件调用）。</summary>
    public void SetTargetStudent(StudentBehaviorController student)
    {
        currentTargetStudent = student;
    }

    /// <summary>由UI或手柄按钮调用：对当前学生发问。</summary>
    public async void AskCurrentStudent()
    {
        if (currentTargetStudent == null)
        {
            Debug.LogWarning("未指定要提问的学生");
            return;
        }

        await AskStudentAboutItem(currentTargetStudent, currentItem);
    }

    /// <summary>对指定学生提出“这是什么”问题，并按概率驱动反应。</summary>
    public async Task AskStudentAboutItem(StudentBehaviorController student, ClassroomItemType itemType)
    {
        if (student == null) return;

        ClassroomItemDefinition definition = itemDefinitions.Find(d => d.itemType == itemType);
        StudentResponseKind response = RollResponse();

        if (response == StudentResponseKind.NoResponse)
        {
            student.SetBehavior(StudentBehaviorType.OffTask, 2f);
            return;
        }

        string line = response == StudentResponseKind.Correct
            ? PickRandom(definition?.correctAnswers, GetDefaultCorrect(itemType))
            : PickRandom(definition?.wrongAnswers, PickRandom(fallbackWrongAnswers, "我不知道"));

        await student.SpeakWithLipSync(line);
        student.SetBehavior(StudentBehaviorType.Listening, postAnswerListenDuration);
    }

    private StudentResponseKind RollResponse()
    {
        float r = Random.value;
        if (r < noResponseProbability) return StudentResponseKind.NoResponse;
        if (r < noResponseProbability + correctResponseProbability) return StudentResponseKind.Correct;
        return StudentResponseKind.Incorrect;
    }

    private string GetDefaultCorrect(ClassroomItemType item)
    {
        switch (item)
        {
            case ClassroomItemType.Cup:
                return "水杯";
            case ClassroomItemType.Pencil:
                return "铅笔";
            case ClassroomItemType.Eraser:
                return "橡皮";
            case ClassroomItemType.Ruler:
                return "尺子";
            default:
                return "这个";
        }
    }

    private string PickRandom(List<string> list, string fallback)
    {
        if (list != null && list.Count > 0)
        {
            return list[Random.Range(0, list.Count)];
        }
        return fallback;
    }
}

