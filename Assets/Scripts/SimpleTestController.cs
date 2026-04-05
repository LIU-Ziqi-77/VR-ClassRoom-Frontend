using UnityEngine;
using System.Collections.Generic;

public class SimpleTestController : MonoBehaviour
{
    [Header("Azure TTS Configuration")]
    [Tooltip("Your Azure Speech Service API Key")]
    public string azureApiKey = "YOUR_AZURE_API_KEY_HERE";
    [Tooltip("Your Azure Speech Service Endpoint")]
    public string azureEndpoint = "https://eastus.api.cognitive.microsoft.com/";
    
    [Header("Test Settings")]
    public bool enableKeyboardControls = true;
    public string[] testPhrases = {
        "Hello, teacher!",
        "I have a question about the lesson.",
        "Can you explain this concept again?",
        "I think I understand now.",
        "Thank you for the explanation."
    };
    
    private StudentBehaviorController[] allStudents;
    private StudentBehaviorController currentStudent;
    private int currentStudentIndex = 0;
    private int currentPhraseIndex = 0;
    
    void Start()
    {
        SetupTTS();
        FindStudents();
        UpdateStatus();
    }
    
    void Update()
    {
        if (enableKeyboardControls)
        {
            HandleKeyboardInput();
        }
    }
    
    private void SetupTTS()
    {
        // 检查是否已有TTSService
        TTSService existingTTS = FindObjectOfType<TTSService>();
        if (existingTTS == null)
        {
            // 创建TTSService
            GameObject ttsGO = new GameObject("TTSService");
            TTSService tts = ttsGO.AddComponent<TTSService>();
            tts.apiKey = azureApiKey;
            tts.endpoint = azureEndpoint;
            
            Debug.Log($"TTSService created with endpoint: {azureEndpoint}");
            if (azureApiKey == "YOUR_AZURE_API_KEY_HERE")
            {
                Debug.LogWarning("⚠️ Please configure your Azure API Key in the SimpleTestController component!");
            }
        }
        else
        {
            // 更新现有TTSService的配置
            existingTTS.apiKey = azureApiKey;
            existingTTS.endpoint = azureEndpoint;
            Debug.Log("Updated existing TTSService configuration");
        }
    }
    
    private void FindStudents()
    {
        allStudents = FindObjectsOfType<StudentBehaviorController>();
        
        if (allStudents.Length > 0)
        {
            currentStudent = allStudents[0];
            Debug.Log($"Found {allStudents.Length} students in the scene");
            
            // 为没有声音配置的学生创建配置
            SetupVoiceConfigs();
        }
        else
        {
            Debug.LogWarning("No StudentBehaviorController found in the scene!");
        }
    }
    
    private void SetupVoiceConfigs()
    {
        foreach (var student in allStudents)
        {
            if (student.voiceConfig == null)
            {
                // 创建声音配置
                StudentVoiceConfig voiceConfig = ScriptableObject.CreateInstance<StudentVoiceConfig>();
                voiceConfig.studentId = student.studentId;
                voiceConfig.studentName = student.studentName;
                
                // 根据姓名猜测性别并分配声音
                string gender = GuessGenderFromName(student.studentName);
                VoiceConfig voice = StudentVoiceConfig.GetRandomVoice(gender);
                voiceConfig.SetVoice(voice.voiceName);
                
                // 设置个性化参数
                voiceConfig.speechRate = Random.Range(0.8f, 1.2f);
                voiceConfig.enthusiasm = Random.Range(0.4f, 0.8f);
                voiceConfig.confidence = Random.Range(0.5f, 0.9f);
                voiceConfig.clarity = Random.Range(0.6f, 0.9f);
                
                student.voiceConfig = voiceConfig;
                
                Debug.Log($"Created voice config for {student.studentName}: {voice.displayName}");
            }
        }
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
        
        // 上下箭头：切换测试短语
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            PreviousPhrase();
        }
        
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            NextPhrase();
        }
        
        // 数字键1-9：直接选择学生
        for (int i = 0; i < Mathf.Min(allStudents.Length, 9); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectStudent(i);
            }
        }
        
        // R键：重新查找学生
        if (Input.GetKeyDown(KeyCode.R))
        {
            FindStudents();
        }
        
        // I键：打印学生信息
        if (Input.GetKeyDown(KeyCode.I))
        {
            PrintStudentInfo();
        }
    }
    
    public void TestSpeak()
    {
        if (currentStudent == null)
        {
            Debug.LogWarning("No student selected!");
            return;
        }
        
        string phrase = testPhrases[currentPhraseIndex];
        Debug.Log($"Testing speech: {currentStudent.studentName} says '{phrase}'");
        
        _ = currentStudent.SpeakWithLipSync(phrase);
    }
    
    public void TestLookAt()
    {
        if (currentStudent == null)
        {
            Debug.LogWarning("No student selected!");
            return;
        }
        
        Debug.Log($"Testing eye gaze: {currentStudent.studentName} looking around");
        
        if (currentStudent.eyeController != null)
        {
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
            Debug.LogWarning("No student selected!");
            return;
        }
        
        StudentBehaviorType[] behaviors = {
            StudentBehaviorType.RaisingHand,
            StudentBehaviorType.TakingNotes,
            StudentBehaviorType.LookingAround,
            StudentBehaviorType.Confused,
            StudentBehaviorType.Excited
        };
        
        StudentBehaviorType randomBehavior = behaviors[Random.Range(0, behaviors.Length)];
        Debug.Log($"Testing behavior: {currentStudent.studentName} - {randomBehavior}");
        
        currentStudent.SetBehavior(randomBehavior, 3f);
    }
    
    public void NextStudent()
    {
        if (allStudents.Length == 0) return;
        
        currentStudentIndex = (currentStudentIndex + 1) % allStudents.Length;
        currentStudent = allStudents[currentStudentIndex];
        UpdateStatus();
    }
    
    public void PreviousStudent()
    {
        if (allStudents.Length == 0) return;
        
        currentStudentIndex = (currentStudentIndex - 1 + allStudents.Length) % allStudents.Length;
        currentStudent = allStudents[currentStudentIndex];
        UpdateStatus();
    }
    
    public void SelectStudent(int index)
    {
        if (index >= 0 && index < allStudents.Length)
        {
            currentStudentIndex = index;
            currentStudent = allStudents[index];
            UpdateStatus();
        }
    }
    
    public void NextPhrase()
    {
        currentPhraseIndex = (currentPhraseIndex + 1) % testPhrases.Length;
        UpdateStatus();
    }
    
    public void PreviousPhrase()
    {
        currentPhraseIndex = (currentPhraseIndex - 1 + testPhrases.Length) % testPhrases.Length;
        UpdateStatus();
    }
    
    private void UpdateStatus()
    {
        if (currentStudent != null)
        {
            string voiceInfo = "";
            if (currentStudent.voiceConfig != null)
            {
                voiceInfo = $" | Voice: {currentStudent.voiceConfig.GetSelectedVoiceConfig().displayName}";
            }
            
            string phraseInfo = $" | Phrase: {testPhrases[currentPhraseIndex]}";
            
            Debug.Log($"Current Student: {currentStudent.studentName} ({currentStudentIndex + 1}/{allStudents.Length}){voiceInfo}{phraseInfo}");
        }
        else
        {
            Debug.Log("No students found!");
        }
    }
    
    private string GuessGenderFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        
        string[] femaleIndicators = { "a", "e", "i", "y", "elle", "ette", "ina", "ine", "ia", "ie", "ey", "ay", "ly", "ry", "ty", "ny", "my", "sy", "cy", "dy", "vy", "zy" };
        string[] maleIndicators = { "o", "n", "r", "t", "d", "l", "c", "m", "p", "b", "k", "g", "h", "j", "q", "v", "w", "x", "z" };
        
        name = name.ToLower();
        
        foreach (string indicator in femaleIndicators)
        {
            if (name.Contains(indicator))
                return "Female";
        }
        
        foreach (string indicator in maleIndicators)
        {
            if (name.Contains(indicator))
                return "Male";
        }
        
        return "";
    }
    
    [ContextMenu("Print Student Info")]
    public void PrintStudentInfo()
    {
        Debug.Log("=== Student Information ===");
        foreach (var student in allStudents)
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
    
    [ContextMenu("Test All Students Speak")]
    public void TestAllStudentsSpeak()
    {
        foreach (var student in allStudents)
        {
            if (student.voiceConfig != null)
            {
                Debug.Log($"Testing {student.studentName} with voice: {student.voiceConfig.GetSelectedVoiceConfig().displayName}");
                _ = student.SpeakWithLipSync("Hello, I am " + student.studentName);
            }
        }
    }
} 