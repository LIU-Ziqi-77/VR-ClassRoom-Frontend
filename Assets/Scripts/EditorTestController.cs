using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EditorTestController : MonoBehaviour
{
    [Header("Test Settings")]
    public bool showTestUI = true;
    public string[] testPhrases = {
        "Hello, teacher!",
        "I have a question about the lesson.",
        "Can you explain this concept again?",
        "I think I understand now.",
        "Thank you for the explanation."
    };
    
    [Header("UI References")]
    public Canvas testCanvas;
    public Text statusText;
    public Button[] studentButtons;
    public Button speakButton;
    public Button lookAtButton;
    public Button behaviorButton;
    public Dropdown phraseDropdown;
    
    private StudentBehaviorController[] students;
    private StudentBehaviorController currentStudent;
    private int currentStudentIndex = 0;
    
    void Start()
    {
        FindStudents();
        SetupUI();
        UpdateStatus();
    }
    
    void Update()
    {
        HandleKeyboardInput();
    }
    
    private void FindStudents()
    {
        students = FindObjectsOfType<StudentBehaviorController>();
        if (students.Length > 0)
        {
            currentStudent = students[0];
        }
    }
    
    private void SetupUI()
    {
        if (!showTestUI) return;
        
        // 创建Canvas
        if (testCanvas == null)
        {
            CreateTestCanvas();
        }
        
        // 设置状态文本
        if (statusText != null)
        {
            statusText.text = "Test Controller Ready";
        }
        
        // 设置短语下拉菜单
        if (phraseDropdown != null)
        {
            phraseDropdown.ClearOptions();
            List<string> options = new List<string>(testPhrases);
            phraseDropdown.AddOptions(options);
        }
    }
    
    private void CreateTestCanvas()
    {
        // 创建Canvas
        GameObject canvasGO = new GameObject("TestCanvas");
        testCanvas = canvasGO.AddComponent<Canvas>();
        testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // 创建状态文本
        GameObject statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(testCanvas.transform);
        statusText = statusGO.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 16;
        statusText.color = Color.white;
        statusText.text = "Test Controller Ready";
        
        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.9f);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.offsetMin = Vector2.zero;
        statusRect.offsetMax = Vector2.zero;
        
        // 创建控制按钮
        CreateControlButtons();
    }
    
    private void CreateControlButtons()
    {
        // 创建按钮容器
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(testCanvas.transform);
        
        RectTransform containerRect = buttonContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(0.3f, 0.3f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;
        
        // 创建按钮
        CreateButton("SpeakButton", "Speak", new Vector2(0.1f, 0.8f), () => TestSpeak());
        CreateButton("LookAtButton", "Look At", new Vector2(0.1f, 0.6f), () => TestLookAt());
        CreateButton("BehaviorButton", "Behavior", new Vector2(0.1f, 0.4f), () => TestBehavior());
        CreateButton("NextStudentButton", "Next Student", new Vector2(0.1f, 0.2f), () => NextStudent());
        CreateButton("PrevStudentButton", "Prev Student", new Vector2(0.1f, 0.0f), () => PreviousStudent());
    }
    
    private void CreateButton(string name, string text, Vector2 position, System.Action onClick)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(testCanvas.transform);
        
        RectTransform buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(position.x, position.y);
        buttonRect.anchorMax = new Vector2(position.x + 0.15f, position.y + 0.15f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        Button button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());
        
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform);
        
        Text buttonText = textGO.AddComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 12;
        buttonText.color = Color.white;
        buttonText.text = text;
        buttonText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
    
    private void HandleKeyboardInput()
    {
        // 空格键：测试说话
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TestSpeak();
        }
        
        // T键：测试眼神
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestLookAt();
        }
        
        // B键：测试行为
        if (Input.GetKeyDown(KeyCode.B))
        {
            TestBehavior();
        }
        
        // 左右箭头：切换学生
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            PreviousStudent();
        }
        
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextStudent();
        }
        
        // 数字键1-4：直接选择学生
        for (int i = 0; i < Mathf.Min(students.Length, 4); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectStudent(i);
            }
        }
    }
    
    public void TestSpeak()
    {
        if (currentStudent == null)
        {
            UpdateStatus("No student selected!");
            return;
        }
        
        string phrase = GetCurrentPhrase();
        UpdateStatus($"Testing speech: {currentStudent.studentName} says '{phrase}'");
        
        // 调用学生的说话功能
        _ = currentStudent.SpeakWithLipSync(phrase);
    }
    
    public void TestLookAt()
    {
        if (currentStudent == null)
        {
            UpdateStatus("No student selected!");
            return;
        }
        
        UpdateStatus($"Testing eye gaze: {currentStudent.studentName} looking around");
        
        // 测试眼神控制
        if (currentStudent.eyeController != null)
        {
            // 随机看向不同方向
            Vector3 randomPosition = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(1f, 2f),
                Random.Range(-5f, 5f)
            );
            
            currentStudent.eyeController.LookAtPosition(randomPosition);
        }
    }
    
    public void TestBehavior()
    {
        if (currentStudent == null)
        {
            UpdateStatus("No student selected!");
            return;
        }
        
        // 随机选择行为
        StudentBehaviorType[] behaviors = {
            StudentBehaviorType.RaisingHand,
            StudentBehaviorType.TakingNotes,
            StudentBehaviorType.LookingAround,
            StudentBehaviorType.Confused,
            StudentBehaviorType.Excited
        };
        
        StudentBehaviorType randomBehavior = behaviors[Random.Range(0, behaviors.Length)];
        
        UpdateStatus($"Testing behavior: {currentStudent.studentName} - {randomBehavior}");
        
        currentStudent.SetBehavior(randomBehavior, 3f);
    }
    
    public void NextStudent()
    {
        if (students.Length == 0) return;
        
        currentStudentIndex = (currentStudentIndex + 1) % students.Length;
        currentStudent = students[currentStudentIndex];
        UpdateStatus();
    }
    
    public void PreviousStudent()
    {
        if (students.Length == 0) return;
        
        currentStudentIndex = (currentStudentIndex - 1 + students.Length) % students.Length;
        currentStudent = students[currentStudentIndex];
        UpdateStatus();
    }
    
    public void SelectStudent(int index)
    {
        if (index >= 0 && index < students.Length)
        {
            currentStudentIndex = index;
            currentStudent = students[index];
            UpdateStatus();
        }
    }
    
    private string GetCurrentPhrase()
    {
        if (phraseDropdown != null && phraseDropdown.value < testPhrases.Length)
        {
            return testPhrases[phraseDropdown.value];
        }
        return testPhrases[0];
    }
    
    private void UpdateStatus(string message = null)
    {
        if (statusText == null) return;
        
        if (message != null)
        {
            statusText.text = message;
        }
        else if (currentStudent != null)
        {
            string voiceInfo = "";
            if (currentStudent.voiceConfig != null)
            {
                voiceInfo = $" | Voice: {currentStudent.voiceConfig.GetSelectedVoiceConfig().displayName}";
            }
            
            statusText.text = $"Current Student: {currentStudent.studentName} ({currentStudentIndex + 1}/{students.Length}){voiceInfo}";
        }
        else
        {
            statusText.text = "No students found!";
        }
    }
    
    // 公共方法供UI调用
    public void OnSpeakButtonClicked()
    {
        TestSpeak();
    }
    
    public void OnLookAtButtonClicked()
    {
        TestLookAt();
    }
    
    public void OnBehaviorButtonClicked()
    {
        TestBehavior();
    }
    
    public void OnNextStudentButtonClicked()
    {
        NextStudent();
    }
    
    public void OnPreviousStudentButtonClicked()
    {
        PreviousStudent();
    }
    
    // 调试信息
    [ContextMenu("Print Student Info")]
    public void PrintStudentInfo()
    {
        Debug.Log("=== Student Information ===");
        foreach (var student in students)
        {
            Debug.Log($"Student: {student.studentName} (ID: {student.studentId})");
            if (student.voiceConfig != null)
            {
                Debug.Log($"  Voice: {student.voiceConfig.GetSelectedVoiceConfig().displayName}");
                Debug.Log($"  Speech Rate: {student.voiceConfig.speechRate}");
                Debug.Log($"  Enthusiasm: {student.voiceConfig.enthusiasm}");
                Debug.Log($"  Confidence: {student.voiceConfig.confidence}");
                Debug.Log($"  Clarity: {student.voiceConfig.clarity}");
            }
        }
    }
} 