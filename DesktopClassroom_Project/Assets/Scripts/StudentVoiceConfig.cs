using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class VoiceConfig
{
    public string voiceName;
    public string displayName;
    public string language;
    public string gender;
    public string description;
    
    public VoiceConfig(string name, string display, string lang, string gen, string desc)
    {
        voiceName = name;
        displayName = display;
        language = lang;
        gender = gen;
        description = desc;
    }
}

[CreateAssetMenu(fileName = "StudentVoiceConfig", menuName = "VR Classroom/Student Voice Configuration")]
public class StudentVoiceConfig : ScriptableObject
{
    [Header("Student Voice Settings")]
    public string studentId;
    public string studentName;
    
    [Header("Voice Selection")]
    [SerializeField] private string selectedVoiceName = "en-US-JennyNeural";
    
    [Header("Voice Parameters")]
    [Range(0.5f, 2.0f)]
    public float speechRate = 1.0f;
    [Range(-50, 50)]
    public int pitchOffset = 0;
    [Range(-50, 50)]
    public int volumeOffset = 0;
    
    [Header("Personality Traits")]
    [Range(0f, 1f)]
    public float enthusiasm = 0.5f;
    [Range(0f, 1f)]
    public float confidence = 0.5f;
    [Range(0f, 1f)]
    public float clarity = 0.5f;
    
    // 英文声音选项（主要使用）
    public static readonly List<VoiceConfig> EnglishVoices = new List<VoiceConfig>
    {
        // 女声
        new VoiceConfig("en-US-JennyNeural", "Jenny (Female)", "en-US", "Female", "Friendly and warm, perfect for casual conversation"),
        new VoiceConfig("en-US-AriaNeural", "Aria (Female)", "en-US", "Female", "Elegant and sophisticated, great for presentations"),
        new VoiceConfig("en-US-SaraNeural", "Sara (Female)", "en-US", "Female", "Clear and professional, ideal for academic settings"),
        new VoiceConfig("en-US-AshleyNeural", "Ashley (Female)", "en-US", "Female", "Young and energetic, perfect for students"),
        new VoiceConfig("en-US-ZiraNeural", "Zira (Female)", "en-US", "Female", "Mature and confident, great for leadership roles"),
        new VoiceConfig("en-US-MichelleNeural", "Michelle (Female)", "en-US", "Female", "Warm and approachable, excellent for teaching"),
        new VoiceConfig("en-US-EmmaNeural", "Emma (Female)", "en-US", "Female", "Bright and enthusiastic, perfect for engaging students"),
        
        // 男声
        new VoiceConfig("en-US-GuyNeural", "Guy (Male)", "en-US", "Male", "Professional and clear, excellent for presentations"),
        new VoiceConfig("en-US-DavisNeural", "Davis (Male)", "en-US", "Male", "Confident and authoritative, great for leadership"),
        new VoiceConfig("en-US-TonyNeural", "Tony (Male)", "en-US", "Male", "Friendly and approachable, perfect for casual interaction"),
        new VoiceConfig("en-US-RyanNeural", "Ryan (Male)", "en-US", "Male", "Young and energetic, ideal for student interactions"),
        new VoiceConfig("en-US-BrianNeural", "Brian (Male)", "en-US", "Male", "Mature and wise, great for mentoring"),
        new VoiceConfig("en-US-JasonNeural", "Jason (Male)", "en-US", "Male", "Clear and articulate, perfect for explanations"),
        new VoiceConfig("en-US-EricNeural", "Eric (Male)", "en-US", "Male", "Calm and reassuring, excellent for guidance")
    };
    
    // 中文声音选项（备用）
    public static readonly List<VoiceConfig> ChineseVoices = new List<VoiceConfig>
    {
        new VoiceConfig("zh-CN-XiaoxiaoNeural", "Xiaoxiao (Female)", "zh-CN", "Female", "Young female voice, lively and cheerful"),
        new VoiceConfig("zh-CN-YunxiNeural", "Yunxi (Male)", "zh-CN", "Male", "Young male voice, energetic and positive")
    };
    
    public string GetSelectedVoiceName()
    {
        return selectedVoiceName;
    }
    
    public void SetVoice(string voiceName)
    {
        selectedVoiceName = voiceName;
    }
    
    public VoiceConfig GetSelectedVoiceConfig()
    {
        foreach (var voice in EnglishVoices)
        {
            if (voice.voiceName == selectedVoiceName)
                return voice;
        }
        
        foreach (var voice in ChineseVoices)
        {
            if (voice.voiceName == selectedVoiceName)
                return voice;
        }
        
        return EnglishVoices[0]; // 默认返回第一个英文声音
    }
    
    public string GetSSMLWithPersonality(string text)
    {
        VoiceConfig voice = GetSelectedVoiceConfig();
        
        // 根据个性特征调整语音参数
        float adjustedRate = speechRate;
        float adjustedPitch = 1.0f + (pitchOffset / 100f);
        float adjustedVolume = 1.0f + (volumeOffset / 100f);
        
        // 根据个性特征进一步调整
        if (enthusiasm > 0.7f)
        {
            adjustedRate *= 1.1f;
            adjustedPitch *= 1.05f;
        }
        
        if (confidence > 0.7f)
        {
            adjustedVolume *= 1.1f;
        }
        
        if (clarity > 0.7f)
        {
            adjustedRate *= 0.95f; // 稍微放慢以提高清晰度
        }
        
        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{voice.language}'>
            <voice name='{voice.voiceName}'>
                <prosody rate='{adjustedRate}' pitch='{adjustedPitch}' volume='{adjustedVolume}'>
                    {text}
                </prosody>
            </voice>
        </speak>";
    }
    
    // 获取所有可用的英文声音
    public static List<VoiceConfig> GetAvailableEnglishVoices()
    {
        return new List<VoiceConfig>(EnglishVoices);
    }
    
    // 获取所有可用的中文声音
    public static List<VoiceConfig> GetAvailableChineseVoices()
    {
        return new List<VoiceConfig>(ChineseVoices);
    }
    
    // 根据性别筛选声音
    public static List<VoiceConfig> GetVoicesByGender(string gender)
    {
        List<VoiceConfig> result = new List<VoiceConfig>();
        
        foreach (var voice in EnglishVoices)
        {
            if (voice.gender.ToLower() == gender.ToLower())
                result.Add(voice);
        }
        
        return result;
    }
    
    // 随机选择一个声音
    public static VoiceConfig GetRandomVoice(string gender = "")
    {
        List<VoiceConfig> availableVoices;
        
        if (string.IsNullOrEmpty(gender))
        {
            availableVoices = EnglishVoices;
        }
        else
        {
            availableVoices = GetVoicesByGender(gender);
        }
        
        if (availableVoices.Count > 0)
        {
            return availableVoices[Random.Range(0, availableVoices.Count)];
        }
        
        return EnglishVoices[0];
    }
} 