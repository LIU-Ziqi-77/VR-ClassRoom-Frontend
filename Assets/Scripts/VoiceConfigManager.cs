using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class VoiceConfigManager : MonoBehaviour
{
    [Header("Voice Configuration Manager")]
    public List<StudentVoiceConfig> studentVoiceConfigs = new List<StudentVoiceConfig>();
    
    [Header("Quick Setup")]
    public bool autoAssignVoices = true;
    public bool matchGenderToAvatar = true;
    
    [Header("Default Settings")]
    public float defaultSpeechRate = 1.0f;
    public float defaultEnthusiasm = 0.5f;
    public float defaultConfidence = 0.5f;
    public float defaultClarity = 0.5f;
    
    private static VoiceConfigManager instance;
    public static VoiceConfigManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<VoiceConfigManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("VoiceConfigManager");
                    instance = go.AddComponent<VoiceConfigManager>();
                }
            }
            return instance;
        }
    }
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    // 为指定学生创建声音配置
    public StudentVoiceConfig CreateVoiceConfig(string studentId, string studentName, string gender = "")
    {
        StudentVoiceConfig config = ScriptableObject.CreateInstance<StudentVoiceConfig>();
        config.studentId = studentId;
        config.studentName = studentName;
        
        // 根据性别选择合适的声音
        if (!string.IsNullOrEmpty(gender))
        {
            VoiceConfig voice = StudentVoiceConfig.GetRandomVoice(gender);
            config.SetVoice(voice.voiceName);
        }
        else
        {
            VoiceConfig voice = StudentVoiceConfig.GetRandomVoice();
            config.SetVoice(voice.voiceName);
        }
        
        // 设置默认参数
        config.speechRate = defaultSpeechRate;
        config.enthusiasm = defaultEnthusiasm;
        config.confidence = defaultConfidence;
        config.clarity = defaultClarity;
        
        return config;
    }
    
    // 获取学生的声音配置
    public StudentVoiceConfig GetVoiceConfig(string studentId)
    {
        foreach (var config in studentVoiceConfigs)
        {
            if (config.studentId == studentId)
                return config;
        }
        return null;
    }
    
    // 自动为所有学生头像分配声音
    public void AutoAssignVoicesToStudents()
    {
        StudentBehaviorController[] students = FindObjectsOfType<StudentBehaviorController>();
        
        foreach (var student in students)
        {
            if (student.voiceConfig == null)
            {
                // 根据学生名称猜测性别（简单实现）
                string gender = GuessGenderFromName(student.studentName);
                
                StudentVoiceConfig config = CreateVoiceConfig(
                    student.studentId, 
                    student.studentName, 
                    gender
                );
                
                student.voiceConfig = config;
                studentVoiceConfigs.Add(config);
                
                Debug.Log($"Assigned voice to {student.studentName}: {config.GetSelectedVoiceConfig().displayName}");
            }
        }
    }
    
    // 根据姓名猜测性别（简单实现）
    private string GuessGenderFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        
        // 这里可以根据中文姓名的特征来猜测性别
        // 这是一个简化的实现，实际项目中可能需要更复杂的逻辑
        
        // 常见的女性名字特征
        string[] femaleIndicators = { "a", "e", "i", "y", "elle", "ette", "ina", "ine", "ia", "ie", "ey", "ay", "ly", "ry", "ty", "ny", "my", "sy", "cy", "dy", "vy", "zy", "anna", "ella", "emma", "sophia", "olivia", "ava", "isabella", "mia", "charlotte", "amelia", "harper", "evelyn", "abigail", "emily", "elizabeth", "sofia", "madison", "avery", "ella", "scarlett", "grace", "chloe", "victoria", "riley", "aria", "lily", "aubrey", "zoey", "penelope", "layla", "nora", "lily", "eleanor", "hannah", "lillian", "addison", "aubrey", "ellie", "stella", "natalie", "zoe", "leah", "hazel", "violet", "aurora", "savannah", "audrey", "brooklyn", "bella", "claire", "skylar", "lucy", "paisley", "everly", "anna", "caroline", "nova", "genesis", "emilia", "kennedy", "samantha", "maya", "willow", "kinsley", "naomi", "aaliyah", "elena", "sarah", "ariyah", "allison", "gabriella", "alice", "madelyn", "cora", "ruby", "eva", "serenity", "autumn", "adeline", "hailey", "gianna", "valentina", "isla", "eliana", "quinn", "nevaeh", "ivy", "sadie", "piper", "lydia", "alexa", "josephine", "emery", "julia", "delilah", "arianna", "vivian", "kaylee", "sophie", "brielle", "madeline", "peyton", "riley", "clara", "haley", "aurora", "savannah", "audrey", "brooklyn", "bella", "claire", "skylar", "lucy", "paisley", "everly", "anna", "caroline", "nova", "genesis", "emilia", "kennedy", "samantha", "maya", "willow", "kinsley", "naomi", "aaliyah", "elena", "sarah", "ariyah", "allison", "gabriella", "alice", "madelyn", "cora", "ruby", "eva", "serenity", "autumn", "adeline", "hailey", "gianna", "valentina", "isla", "eliana", "quinn", "nevaeh", "ivy", "sadie", "piper", "lydia", "alexa", "josephine", "emery", "julia", "delilah", "arianna", "vivian", "kaylee", "sophie", "brielle", "madeline", "peyton", "riley", "clara", "haley" };
        
        // 常见的男性名字特征
        string[] maleIndicators = { "o", "n", "r", "t", "d", "l", "c", "m", "p", "b", "k", "g", "h", "j", "q", "v", "w", "x", "z", "er", "or", "ar", "ir", "ur", "an", "en", "in", "on", "un", "al", "el", "il", "ol", "ul", "as", "es", "is", "os", "us", "at", "et", "it", "ot", "ut", "ad", "ed", "id", "od", "ud", "am", "em", "im", "om", "um", "ap", "ep", "ip", "op", "up", "ab", "eb", "ib", "ob", "ub", "ac", "ec", "ic", "oc", "uc", "af", "ef", "if", "of", "uf", "ag", "eg", "ig", "og", "ug", "ah", "eh", "ih", "oh", "uh", "aj", "ej", "ij", "oj", "uj", "ak", "ek", "ik", "ok", "uk", "al", "el", "il", "ol", "ul", "am", "em", "im", "om", "um", "an", "en", "in", "on", "un", "ap", "ep", "ip", "op", "up", "ar", "er", "ir", "or", "ur", "as", "es", "is", "os", "us", "at", "et", "it", "ot", "ut", "av", "ev", "iv", "ov", "uv", "aw", "ew", "iw", "ow", "uw", "ax", "ex", "ix", "ox", "ux", "ay", "ey", "iy", "oy", "uy", "az", "ez", "iz", "oz", "uz", "noah", "liam", "oliver", "elijah", "william", "james", "benjamin", "lucas", "henry", "theodore", "jack", "levi", "sebastian", "daniel", "jackson", "samuel", "matthew", "david", "joseph", "carter", "owen", "wyatt", "john", "jack", "luke", "jayden", "dylan", "grayson", "isaac", "anthony", "christopher", "andrew", "joshua", "christian", "mason", "adrian", "leo", "colton", "hudson", "julian", "aaron", "eli", "landon", "jonathan", "nathan", "isaiah", "charles", "thomas", "christopher", "jaxon", "hunter", "levi", "eli", "sebastian", "daniel", "jackson", "samuel", "matthew", "david", "joseph", "carter", "owen", "wyatt", "john", "jack", "luke", "jayden", "dylan", "grayson", "isaac", "anthony", "christopher", "andrew", "joshua", "christian", "mason", "adrian", "leo", "colton", "hudson", "julian", "aaron", "eli", "landon", "jonathan", "nathan", "isaiah", "charles", "thomas", "christopher", "jaxon", "hunter", "levi", "eli", "sebastian", "daniel", "jackson", "samuel", "matthew", "david", "joseph", "carter", "owen", "wyatt", "john", "jack", "luke", "jayden", "dylan", "grayson", "isaac", "anthony", "christopher", "andrew", "joshua", "christian", "mason", "adrian", "leo", "colton", "hudson", "julian", "aaron", "eli", "landon", "jonathan", "nathan", "isaiah", "charles", "thomas", "christopher", "jaxon", "hunter" };
        
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
        
        return ""; // 无法确定
    }
    
    // 保存所有声音配置到文件
    public void SaveVoiceConfigs()
    {
        #if UNITY_EDITOR
        foreach (var config in studentVoiceConfigs)
        {
            if (config != null)
            {
                string path = $"Assets/VoiceConfigs/{config.studentName}_VoiceConfig.asset";
                
                // 确保目录存在
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        #endif
    }
    
    // 加载所有声音配置
    public void LoadVoiceConfigs()
    {
        #if UNITY_EDITOR
        studentVoiceConfigs.Clear();
        
        string[] guids = AssetDatabase.FindAssets("t:StudentVoiceConfig");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StudentVoiceConfig config = AssetDatabase.LoadAssetAtPath<StudentVoiceConfig>(path);
            if (config != null)
            {
                studentVoiceConfigs.Add(config);
            }
        }
        #endif
    }
    
            // 测试所有学生的声音
        public void TestAllVoices()
        {
            foreach (var config in studentVoiceConfigs)
            {
                if (config != null)
                {
                    Debug.Log($"Testing {config.studentName}'s voice: {config.GetSelectedVoiceConfig().displayName}");
                    // 这里可以添加实际的语音测试逻辑
                }
            }
        }
    
    // 随机化所有学生的声音参数
    public void RandomizeVoiceParameters()
    {
        foreach (var config in studentVoiceConfigs)
        {
            if (config != null)
            {
                config.speechRate = Random.Range(0.8f, 1.2f);
                config.pitchOffset = Random.Range(-20, 20);
                config.volumeOffset = Random.Range(-10, 10);
                config.enthusiasm = Random.Range(0.3f, 0.8f);
                config.confidence = Random.Range(0.4f, 0.9f);
                config.clarity = Random.Range(0.5f, 0.9f);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VoiceConfigManager))]
public class VoiceConfigManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        VoiceConfigManager manager = (VoiceConfigManager)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("快速操作", EditorStyles.boldLabel);
        
        if (GUILayout.Button("自动分配声音"))
        {
            manager.AutoAssignVoicesToStudents();
        }
        
        if (GUILayout.Button("保存声音配置"))
        {
            manager.SaveVoiceConfigs();
        }
        
        if (GUILayout.Button("加载声音配置"))
        {
            manager.LoadVoiceConfigs();
        }
        
        if (GUILayout.Button("测试所有声音"))
        {
            manager.TestAllVoices();
        }
        
        if (GUILayout.Button("随机化声音参数"))
        {
            manager.RandomizeVoiceParameters();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Available Voices", EditorStyles.boldLabel);
        
        // 显示英文声音
        EditorGUILayout.LabelField("English Voices:", EditorStyles.boldLabel);
        foreach (var voice in StudentVoiceConfig.GetAvailableEnglishVoices())
        {
            EditorGUILayout.LabelField($"{voice.displayName} - {voice.description}");
        }
        
        // 显示中文声音（备用）
        EditorGUILayout.LabelField("Chinese Voices (Backup):", EditorStyles.boldLabel);
        foreach (var voice in StudentVoiceConfig.GetAvailableChineseVoices())
        {
            EditorGUILayout.LabelField($"{voice.displayName} - {voice.description}");
        }
    }
}
#endif 