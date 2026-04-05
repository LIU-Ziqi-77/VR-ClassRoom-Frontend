using UnityEngine;
using System.Collections;

public class QuickTestSetup : MonoBehaviour
{
    [Header("🚀 快速TTS测试设置")]
    [Tooltip("您的Azure语音服务API密钥")]
    public string azureApiKey = "YOUR_AZURE_API_KEY_HERE";
    
    [Header("🎭 测试用学生Avatar")]
    public Transform[] studentPositions;
    public string[] studentNames = { "小明", "小红", "小强", "小美" };
    
    [Header("🎤 测试语音内容")]
    public string[] testPhrases = {
        "大家好，我是学生！",
        "老师，我有一个问题。",
        "这个概念我明白了。", 
        "谢谢老师的解释！",
        "我想回答这个问题。"
    };
    
    private StudentBehaviorController[] students;
    private int currentStudentIndex = 0;
    private int currentPhraseIndex = 0;
    
    void Start()
    {
        Debug.Log("🎯 TTS快速测试已启动！");
        SetupTTSService();
        FindOrCreateStudents();
        ShowInstructions();
    }
    
    void Update()
    {
        HandleKeyboardInput();
    }
    
    private void SetupTTSService()
    {
        TTSService tts = FindObjectOfType<TTSService>();
        if (tts == null)
        {
            GameObject ttsGO = new GameObject("TTSService");
            tts = ttsGO.AddComponent<TTSService>();
        }
        
        tts.apiKey = azureApiKey;
        
        if (azureApiKey == "YOUR_AZURE_API_KEY_HERE")
        {
            Debug.LogError("❌ 请在QuickTestSetup组件中设置您的Azure API密钥！");
        }
        else
        {
            Debug.Log("✅ TTS服务已配置完成");
        }
    }
    
    private void FindOrCreateStudents()
    {
        students = FindObjectsOfType<StudentBehaviorController>();
        
        if (students.Length == 0)
        {
            Debug.LogWarning("⚠️ 场景中没有找到学生Avatar，请确保有StudentBehaviorController组件");
        }
        else
        {
            Debug.Log($"✅ 找到 {students.Length} 个学生Avatar");
            
            // 为学生分配不同的声音
            for (int i = 0; i < students.Length; i++)
            {
                if (students[i].voiceConfig == null)
                {
                    AssignVoiceToStudent(students[i], i);
                }
            }
        }
    }
    
    private void AssignVoiceToStudent(StudentBehaviorController student, int index)
    {
        StudentVoiceConfig voiceConfig = ScriptableObject.CreateInstance<StudentVoiceConfig>();
        voiceConfig.studentId = student.studentId;
        voiceConfig.studentName = studentNames[index % studentNames.Length];
        
        // 根据索引分配不同的声音
        string[] voices = { 
            "en-US-JennyNeural", "en-US-GuyNeural", 
            "en-US-AriaNeural", "en-US-DavisNeural",
            "en-US-SaraNeural", "en-US-TonyNeural"
        };
        
        voiceConfig.SetVoice(voices[index % voices.Length]);
        voiceConfig.speechRate = 0.9f + (index * 0.1f); // 不同语速
        voiceConfig.enthusiasm = 0.5f + (index * 0.1f); // 不同热情度
        
        student.voiceConfig = voiceConfig;
        
        Debug.Log($"🎭 为学生 {student.studentName} 分配了声音: {voiceConfig.GetSelectedVoiceConfig().displayName}");
    }
    
    private void HandleKeyboardInput()
    {
        // 空格键：当前学生说话
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TestCurrentStudentSpeak();
        }
        
        // 数字键1-9：选择学生
        for (int i = 0; i < Mathf.Min(students.Length, 9); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectStudent(i);
            }
        }
        
        // A键：所有学生依次说话
        if (Input.GetKeyDown(KeyCode.A))
        {
            StartCoroutine(AllStudentsSpeak());
        }
        
        // T键：切换测试短语
        if (Input.GetKeyDown(KeyCode.T))
        {
            NextPhrase();
        }
        
        // H键：显示帮助
        if (Input.GetKeyDown(KeyCode.H))
        {
            ShowInstructions();
        }
        
        // R键：随机测试
        if (Input.GetKeyDown(KeyCode.R))
        {
            RandomTest();
        }
    }
    
    private void TestCurrentStudentSpeak()
    {
        if (students.Length == 0)
        {
            Debug.LogWarning("⚠️ 没有可用的学生Avatar");
            return;
        }
        
        StudentBehaviorController student = students[currentStudentIndex];
        string phrase = testPhrases[currentPhraseIndex];
        
        Debug.Log($"🎤 {student.studentName} 说: '{phrase}'");
        _ = student.SpeakWithLipSync(phrase);
    }
    
    private void SelectStudent(int index)
    {
        if (index < students.Length)
        {
            currentStudentIndex = index;
            Debug.Log($"👤 选择了学生: {students[index].studentName}");
        }
    }
    
    private void NextPhrase()
    {
        currentPhraseIndex = (currentPhraseIndex + 1) % testPhrases.Length;
        Debug.Log($"💬 切换到短语: '{testPhrases[currentPhraseIndex]}'");
    }
    
    private IEnumerator AllStudentsSpeak()
    {
        Debug.Log("🎭 所有学生将依次说话...");
        
        for (int i = 0; i < students.Length; i++)
        {
            string phrase = testPhrases[i % testPhrases.Length];
            Debug.Log($"🎤 {students[i].studentName}: '{phrase}'");
            
            _ = students[i].SpeakWithLipSync(phrase);
            
            // 等待3秒再让下一个学生说话
            yield return new WaitForSeconds(3f);
        }
        
        Debug.Log("✅ 所有学生都说完了！");
    }
    
    private void RandomTest()
    {
        if (students.Length == 0) return;
        
        // 随机选择学生和短语
        int randomStudent = Random.Range(0, students.Length);
        int randomPhrase = Random.Range(0, testPhrases.Length);
        
        currentStudentIndex = randomStudent;
        currentPhraseIndex = randomPhrase;
        
        TestCurrentStudentSpeak();
    }
    
    private void ShowInstructions()
    {
        Debug.Log("🎯 TTS测试快捷键说明：");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log("⌨️  空格键 - 当前学生说话");
        Debug.Log("⌨️  数字键1-9 - 选择对应学生");
        Debug.Log("⌨️  A键 - 所有学生依次说话");
        Debug.Log("⌨️  T键 - 切换测试短语");
        Debug.Log("⌨️  R键 - 随机测试");
        Debug.Log("⌨️  H键 - 显示帮助");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        if (students.Length > 0)
        {
            Debug.Log($"👤 当前选择: {students[currentStudentIndex].studentName}");
            Debug.Log($"💬 当前短语: '{testPhrases[currentPhraseIndex]}'");
        }
    }
} 