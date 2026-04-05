using UnityEngine;
using UnityEngine.UI;

public class StudentTestController : MonoBehaviour
{
    [Header("UI References")]
    public InputField textInput;
    public Button speakButton;
    public Button lookAtTeacherButton;
    public Button lookAtStudentButton;
    public Button raiseHandButton;
    public Button resetButton;
    public Dropdown studentDropdown;
    public Dropdown behaviorDropdown;
    
    [Header("Test Settings")]
    public string[] testTexts = {
        "大家好，我是学生小明",
        "老师，我有一个问题",
        "这个问题很有趣",
        "我明白了，谢谢老师"
    };
    
    private StudentBehaviorController[] allStudents;
    private StudentBehaviorController currentStudent;
    
    void Start()
    {
        InitializeUI();
        FindAllStudents();
        SetupEventListeners();
    }
    
    private void InitializeUI()
    {
        // 初始化行为下拉菜单
        behaviorDropdown.ClearOptions();
        var behaviorNames = new System.Collections.Generic.List<string>(System.Enum.GetNames(typeof(StudentBehaviorType)));
        behaviorDropdown.AddOptions(behaviorNames);
        
        // 设置默认文本
        if (textInput != null)
        {
            textInput.text = testTexts[0];
        }
    }
    
    private void FindAllStudents()
    {
        allStudents = FindObjectsOfType<StudentBehaviorController>();
        
        // 初始化学生下拉菜单
        studentDropdown.ClearOptions();
        var studentNames = new System.Collections.Generic.List<string>();
        
        foreach (var student in allStudents)
        {
            string displayName = $"{student.GetStudentName()} ({student.GetStudentId()})";
            studentNames.Add(displayName);
        }
        
        studentDropdown.AddOptions(studentNames);
        
        // 设置默认学生
        if (allStudents.Length > 0)
        {
            currentStudent = allStudents[0];
        }
    }
    
    private void SetupEventListeners()
    {
        if (speakButton != null)
        {
            speakButton.onClick.AddListener(OnSpeakButtonClicked);
        }
        
        if (lookAtTeacherButton != null)
        {
            lookAtTeacherButton.onClick.AddListener(OnLookAtTeacherButtonClicked);
        }
        
        if (lookAtStudentButton != null)
        {
            lookAtStudentButton.onClick.AddListener(OnLookAtStudentButtonClicked);
        }
        
        if (raiseHandButton != null)
        {
            raiseHandButton.onClick.AddListener(OnRaiseHandButtonClicked);
        }
        
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetButtonClicked);
        }
        
        if (studentDropdown != null)
        {
            studentDropdown.onValueChanged.AddListener(OnStudentDropdownChanged);
        }
        
        if (behaviorDropdown != null)
        {
            behaviorDropdown.onValueChanged.AddListener(OnBehaviorDropdownChanged);
        }
    }
    
    private void OnSpeakButtonClicked()
    {
        if (currentStudent == null) return;
        
        string text = textInput != null ? textInput.text : testTexts[0];
        if (!string.IsNullOrEmpty(text))
        {
            currentStudent.SpeakWithLipSync(text);
        }
    }
    
    private void OnLookAtTeacherButtonClicked()
    {
        if (currentStudent == null) return;
        
        currentStudent.LookAtTeacher();
    }
    
    private void OnLookAtStudentButtonClicked()
    {
        if (currentStudent == null) return;
        
        // 随机选择一个其他学生
        var otherStudents = System.Array.FindAll(allStudents, s => s != currentStudent);
        if (otherStudents.Length > 0)
        {
            var randomStudent = otherStudents[Random.Range(0, otherStudents.Length)];
            currentStudent.LookAtStudent(randomStudent.GetStudentId());
        }
    }
    
    private void OnRaiseHandButtonClicked()
    {
        if (currentStudent == null) return;
        
        currentStudent.SetBehavior(StudentBehaviorType.RaisingHand, 3f);
    }
    
    private void OnResetButtonClicked()
    {
        if (currentStudent == null) return;
        
        currentStudent.StopCurrentBehavior();
        currentStudent.ResetEyeDirection();
    }
    
    private void OnStudentDropdownChanged(int index)
    {
        if (index >= 0 && index < allStudents.Length)
        {
            currentStudent = allStudents[index];
            Debug.Log($"Selected student: {currentStudent.GetStudentName()}");
        }
    }
    
    private void OnBehaviorDropdownChanged(int index)
    {
        if (currentStudent == null) return;
        
        StudentBehaviorType behavior = (StudentBehaviorType)index;
        currentStudent.SetBehavior(behavior, 5f);
    }
    
    // 测试方法
    public void TestRandomBehavior()
    {
        if (currentStudent == null) return;
        
        StudentBehaviorType[] behaviors = {
            StudentBehaviorType.Listening,
            StudentBehaviorType.RaisingHand,
            StudentBehaviorType.TakingNotes,
            StudentBehaviorType.LookingAround,
            StudentBehaviorType.Confused,
            StudentBehaviorType.Excited
        };
        
        StudentBehaviorType randomBehavior = behaviors[Random.Range(0, behaviors.Length)];
        currentStudent.SetBehavior(randomBehavior, Random.Range(3f, 8f));
    }
    
    public void TestRandomSpeech()
    {
        if (currentStudent == null) return;
        
        string randomText = testTexts[Random.Range(0, testTexts.Length)];
        currentStudent.SpeakWithLipSync(randomText);
    }
    
    public void TestAllStudentsSpeak()
    {
        string text = "大家好，我是学生";
        
        for (int i = 0; i < allStudents.Length; i++)
        {
            var student = allStudents[i];
            string studentText = $"{text} {student.GetStudentName()}";
            
            // 延迟播放，避免重叠
            StartCoroutine(DelayedSpeech(student, studentText, i * 2f));
        }
    }
    
    private System.Collections.IEnumerator DelayedSpeech(StudentBehaviorController student, string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        student.SpeakWithLipSync(text);
    }
    
    public void TestNetworkMessage()
    {
        if (StudentNetworkManager.Instance != null)
        {
            StudentNetworkManager.Instance.SendTestMessage();
        }
    }
    
    // 键盘快捷键
    void Update()
    {
        // 数字键1-4选择学生
        for (int i = 0; i < Mathf.Min(4, allStudents.Length); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                currentStudent = allStudents[i];
                studentDropdown.value = i;
                Debug.Log($"Selected student {i + 1}: {currentStudent.GetStudentName()}");
            }
        }
        
        // 空格键说话
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnSpeakButtonClicked();
        }
        
        // T键看向教师
        if (Input.GetKeyDown(KeyCode.T))
        {
            OnLookAtTeacherButtonClicked();
        }
        
        // R键举手
        if (Input.GetKeyDown(KeyCode.R))
        {
            OnRaiseHandButtonClicked();
        }
        
        // X键重置
        if (Input.GetKeyDown(KeyCode.X))
        {
            OnResetButtonClicked();
        }
        
        // B键随机行为
        if (Input.GetKeyDown(KeyCode.B))
        {
            TestRandomBehavior();
        }
        
        // S键随机说话
        if (Input.GetKeyDown(KeyCode.S))
        {
            TestRandomSpeech();
        }
        
        // A键所有学生说话
        if (Input.GetKeyDown(KeyCode.A))
        {
            TestAllStudentsSpeak();
        }
    }
} 