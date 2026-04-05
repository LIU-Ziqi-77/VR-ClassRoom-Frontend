using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;
using System.Threading.Tasks;

[System.Serializable]
public class TTSRequest
{
    public string text;
    public string voice = "en-US-JennyNeural"; // 默认英文女声
    public float rate = 1.0f;
    public float pitch = 1.0f;
}

[System.Serializable]
public class TTSResponse
{
    public string audioContent;
}

public class TTSService : MonoBehaviour
{
    [Header("TTS Settings")]
    public string apiKey = "YOUR_AZURE_API_KEY_HERE"; // ⚠️ 请在这里填入您的Azure API密钥
    public string endpoint = "https://eastus.api.cognitive.microsoft.com/";
    
    [Header("Audio Settings")]
    public AudioSource audioSource;
    
    private static TTSService instance;
    public static TTSService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TTSService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TTSService");
                    instance = go.AddComponent<TTSService>();
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
    
    public async Task<AudioClip> GenerateSpeech(string text, string voice = "zh-CN-XiaoxiaoNeural")
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("TTS API Key not set!");
            return null;
        }
        
        var request = new TTSRequest
        {
            text = text,
            voice = voice
        };
        
        return await GenerateSpeechAsync(request);
    }
    
    public async Task<AudioClip> GenerateSpeechWithSSML(string ssml)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("TTS API Key not set!");
            return null;
        }
        
        return await GenerateSpeechWithSSMLAsync(ssml);
    }
    
    private async Task<AudioClip> GenerateSpeechAsync(TTSRequest request)
    {
        // 构建SSML
        string ssml = BuildSSML(request);
        
        // Azure AI Foundry的TTS API路径
        string ttsEndpoint = endpoint.TrimEnd('/') + "/speech/tts/v1";
        
        using (UnityWebRequest webRequest = new UnityWebRequest(ttsEndpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(ssml);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/ssml+xml");
            webRequest.SetRequestHeader("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
            webRequest.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
            webRequest.SetRequestHeader("User-Agent", "UnityTTS");
            
            var operation = webRequest.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                return ConvertAudioDataToClip(webRequest.downloadHandler.data);
            }
            else
            {
                Debug.LogError($"TTS Error: {webRequest.error}");
                return null;
            }
        }
    }
    
    private async Task<AudioClip> GenerateSpeechWithSSMLAsync(string ssml)
    {
        // Azure AI Foundry的TTS API路径
        string ttsEndpoint = endpoint.TrimEnd('/') + "/speech/tts/v1";
        
        using (UnityWebRequest webRequest = new UnityWebRequest(ttsEndpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(ssml);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/ssml+xml");
            webRequest.SetRequestHeader("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
            webRequest.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey);
            webRequest.SetRequestHeader("User-Agent", "UnityTTS");
            
            var operation = webRequest.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                return ConvertAudioDataToClip(webRequest.downloadHandler.data);
            }
            else
            {
                Debug.LogError($"TTS Error: {webRequest.error}");
                return null;
            }
        }
    }
    
    private string BuildSSML(TTSRequest request)
    {
        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'>
            <voice name='{request.voice}'>
                <prosody rate='{request.rate}' pitch='{request.pitch}'>
                    {request.text}
                </prosody>
            </voice>
        </speak>";
    }
    
    private AudioClip ConvertAudioDataToClip(byte[] audioData)
    {
        // 将WAV格式的音频数据转换为AudioClip
        // 这里简化处理，实际项目中可能需要更复杂的音频格式转换
        WAV wav = new WAV(audioData);
        AudioClip audioClip = AudioClip.Create("TTS", wav.SampleCount, 1, wav.Frequency, false);
        audioClip.SetData(wav.LeftChannel, 0);
        return audioClip;
    }
}

// WAV文件解析类
public class WAV
{
    public float[] LeftChannel { get; internal set; }
    public float[] RightChannel { get; internal set; }
    public int Frequency { get; internal set; }
    public int SampleCount { get; internal set; }
    
    public WAV(byte[] wav)
    {
        // 解析WAV文件头
        Frequency = BitConverter.ToInt32(wav, 24);
        int channels = BitConverter.ToInt16(wav, 22);
        int bitsPerSample = BitConverter.ToInt16(wav, 34);
        
        // 找到数据开始位置
        int dataStart = 44;
        for (int i = 0; i < wav.Length - 4; i++)
        {
            if (wav[i] == 'd' && wav[i + 1] == 'a' && wav[i + 2] == 't' && wav[i + 3] == 'a')
            {
                dataStart = i + 8;
                break;
            }
        }
        
        int dataLength = BitConverter.ToInt32(wav, dataStart - 4);
        SampleCount = dataLength / (bitsPerSample / 8) / channels;
        
        LeftChannel = new float[SampleCount];
        RightChannel = new float[SampleCount];
        
        int sampleIndex = 0;
        for (int i = dataStart; i < dataStart + dataLength; i += bitsPerSample / 8 * channels)
        {
            if (bitsPerSample == 16)
            {
                LeftChannel[sampleIndex] = BitConverter.ToInt16(wav, i) / 32768f;
                if (channels == 2)
                    RightChannel[sampleIndex] = BitConverter.ToInt16(wav, i + 2) / 32768f;
            }
            sampleIndex++;
        }
    }
} 