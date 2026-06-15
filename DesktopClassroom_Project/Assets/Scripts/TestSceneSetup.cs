using UnityEngine;
using System.Collections.Generic;

public class TestSceneSetup : MonoBehaviour
{
    [Header("Test Scene Setup")]
    public bool autoSetupOnStart = true;
    public int numberOfStudents = 4;
    public Vector3[] studentPositions = new Vector3[]
    {
        new Vector3(-3, 0, 2),
        new Vector3(3, 0, 2),
        new Vector3(-3, 0, -2),
        new Vector3(3, 0, -2)
    };
    
    [Header("Azure TTS Configuration")]
    [Tooltip("Your Azure Speech Service API Key")]
    public string azureApiKey = "YOUR_AZURE_API_KEY_HERE";
    [Tooltip("Your Azure Speech Service Endpoint")]
    public string azureEndpoint = "https://eastus.api.cognitive.microsoft.com/";
    
    [Header("Test Components")]
    public GameObject studentPrefab;
    public GameObject teacherPosition;
    public GameObject cameraRig;
    
    [Header("Test Settings")]
    public string[] testStudentNames = { "Emma", "Ryan", "Sara", "Tony" };
    public string[] testStudentIds = { "student_001", "student_002", "student_003", "student_004" };
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            SetupTestScene();
        }
    }
    
    [ContextMenu("Setup Test Scene")]
    public void SetupTestScene()
    {
        Debug.Log("Setting up test scene...");
        
        // 创建TTSService
        CreateTTSService();
        
        // 创建VoiceConfigManager
        CreateVoiceConfigManager();
        
        // 创建测试学生
        CreateTestStudents();
        
        // 创建教师位置
        CreateTeacherPosition();
        
        // 创建测试控制器
        CreateTestController();
        
        // 创建简单的环境
        CreateSimpleEnvironment();
        
        Debug.Log("Test scene setup complete!");
    }
    
    private void CreateTTSService()
    {
        if (FindObjectOfType<TTSService>() == null)
        {
            GameObject ttsGO = new GameObject("TTSService");
            TTSService tts = ttsGO.AddComponent<TTSService>();
            
            // 使用Inspector中配置的Azure设置
            tts.apiKey = azureApiKey;
            tts.endpoint = azureEndpoint;
            
            Debug.Log($"TTSService created with endpoint: {azureEndpoint}");
            if (azureApiKey == "YOUR_AZURE_API_KEY_HERE")
            {
                Debug.LogWarning("⚠️ Please configure your Azure API Key in the TestSceneSetup component!");
            }
        }
    }
    
    private void CreateVoiceConfigManager()
    {
        if (FindObjectOfType<VoiceConfigManager>() == null)
        {
            GameObject vcmGO = new GameObject("VoiceConfigManager");
            VoiceConfigManager vcm = vcmGO.AddComponent<VoiceConfigManager>();
            
            // 设置默认参数
            vcm.defaultSpeechRate = 1.0f;
            vcm.defaultEnthusiasm = 0.6f;
            vcm.defaultConfidence = 0.7f;
            vcm.defaultClarity = 0.8f;
            
            Debug.Log("VoiceConfigManager created.");
        }
    }
    
    private void CreateTestStudents()
    {
        // 清理现有的测试学生
        GameObject[] existingStudents = GameObject.FindGameObjectsWithTag("Student");
        foreach (var student in existingStudents)
        {
            if (student.name.Contains("TestStudent"))
            {
                DestroyImmediate(student);
            }
        }
        
        // 创建新的测试学生
        for (int i = 0; i < Mathf.Min(numberOfStudents, testStudentNames.Length); i++)
        {
            CreateTestStudent(i);
        }
    }
    
    private void CreateTestStudent(int index)
    {
        GameObject studentGO = new GameObject($"TestStudent_{testStudentNames[index]}");
        studentGO.tag = "Student";
        studentGO.transform.position = studentPositions[index];
        
        // 添加基本组件
        StudentBehaviorController behaviorController = studentGO.AddComponent<StudentBehaviorController>();
        LipSyncController lipSyncController = studentGO.AddComponent<LipSyncController>();
        EyeController eyeController = studentGO.AddComponent<EyeController>();
        
        // 设置学生信息
        behaviorController.studentId = testStudentIds[index];
        behaviorController.studentName = testStudentNames[index];
        
        // 创建声音配置
        StudentVoiceConfig voiceConfig = ScriptableObject.CreateInstance<StudentVoiceConfig>();
        voiceConfig.studentId = testStudentIds[index];
        voiceConfig.studentName = testStudentNames[index];
        
        // 根据性别分配声音
        string gender = GuessGenderFromName(testStudentNames[index]);
        VoiceConfig voice = StudentVoiceConfig.GetRandomVoice(gender);
        voiceConfig.SetVoice(voice.voiceName);
        
        // 设置个性化参数
        voiceConfig.speechRate = Random.Range(0.8f, 1.2f);
        voiceConfig.enthusiasm = Random.Range(0.4f, 0.8f);
        voiceConfig.confidence = Random.Range(0.5f, 0.9f);
        voiceConfig.clarity = Random.Range(0.6f, 0.9f);
        
        behaviorController.voiceConfig = voiceConfig;
        
        // 创建简单的视觉表示（立方体）
        CreateStudentVisual(studentGO, index);
        
        Debug.Log($"Created test student: {testStudentNames[index]} with voice: {voice.displayName}");
    }
    
    private void CreateStudentVisual(GameObject studentGO, int index)
    {
        // 创建身体
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(studentGO.transform);
        body.transform.localPosition = Vector3.up * 0.5f;
        body.transform.localScale = new Vector3(0.5f, 1f, 0.3f);
        
        // 创建头部
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(studentGO.transform);
        head.transform.localPosition = Vector3.up * 1.5f;
        head.transform.localScale = Vector3.one * 0.3f;
        
        // 设置颜色
        Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        Renderer headRenderer = head.GetComponent<Renderer>();
        
        if (index < colors.Length)
        {
            bodyRenderer.material.color = colors[index];
            headRenderer.material.color = colors[index] * 0.8f;
        }
        
        // 添加标签显示
        CreateNameTag(studentGO, index);
    }
    
    private void CreateNameTag(GameObject studentGO, int index)
    {
        GameObject nameTag = new GameObject("NameTag");
        nameTag.transform.SetParent(studentGO.transform);
        nameTag.transform.localPosition = Vector3.up * 2.2f;
        
        // 创建文本显示（简单的3D文本）
        GameObject textGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        textGO.transform.SetParent(nameTag.transform);
        textGO.transform.localScale = new Vector3(2f, 0.5f, 1f);
        textGO.transform.LookAt(Camera.main.transform);
        
        // 设置文本材质
        Renderer textRenderer = textGO.GetComponent<Renderer>();
        textRenderer.material = new Material(Shader.Find("Standard"));
        textRenderer.material.color = Color.white;
    }
    
    private void CreateTeacherPosition()
    {
        if (teacherPosition == null)
        {
            teacherPosition = new GameObject("TeacherPosition");
            teacherPosition.transform.position = new Vector3(0, 0, 5);
            
            // 创建教师位置指示器
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "TeacherIndicator";
            indicator.transform.SetParent(teacherPosition.transform);
            indicator.transform.localPosition = Vector3.zero;
            indicator.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            
            Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
            indicatorRenderer.material.color = Color.magenta;
        }
    }
    
    private void CreateTestController()
    {
        if (FindObjectOfType<StudentTestController>() == null)
        {
            GameObject testControllerGO = new GameObject("StudentTestController");
            StudentTestController testController = testControllerGO.AddComponent<StudentTestController>();
            
            Debug.Log("StudentTestController created. Use Space/T/B keys to test.");
        }
    }
    
    private void CreateSimpleEnvironment()
    {
        // 创建地面
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = Vector3.one * 2f;
        
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        groundRenderer.material.color = new Color(0.8f, 0.8f, 0.8f);
        
        // 创建墙壁
        CreateWall("Wall_Back", new Vector3(0, 2, -5), new Vector3(10, 4, 0.1f));
        CreateWall("Wall_Left", new Vector3(-5, 2, 0), new Vector3(0.1f, 4, 10));
        CreateWall("Wall_Right", new Vector3(5, 2, 0), new Vector3(0.1f, 4, 10));
    }
    
    private void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        
        Renderer wallRenderer = wall.GetComponent<Renderer>();
        wallRenderer.material.color = new Color(0.9f, 0.9f, 0.9f);
    }
    
    private string GuessGenderFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        
        // 简化的英文姓名性别判断
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
    
    [ContextMenu("Test All Students")]
    public void TestAllStudents()
    {
        StudentBehaviorController[] students = FindObjectsOfType<StudentBehaviorController>();
        
        foreach (var student in students)
        {
            if (student.voiceConfig != null)
            {
                Debug.Log($"Student: {student.studentName}, Voice: {student.voiceConfig.GetSelectedVoiceConfig().displayName}");
            }
        }
    }
    
    [ContextMenu("Auto Assign Voices")]
    public void AutoAssignVoices()
    {
        VoiceConfigManager vcm = FindObjectOfType<VoiceConfigManager>();
        if (vcm != null)
        {
            vcm.AutoAssignVoicesToStudents();
        }
    }
} 