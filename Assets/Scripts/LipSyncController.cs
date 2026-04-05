using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRM;

[System.Serializable]
public class LipSyncFrame
{
    public float aValue; // 张嘴程度
    public float iValue; // 微笑程度
    public float uValue; // 嘟嘴程度
    public float eValue; // 咧嘴程度
    public float oValue; // 圆嘴程度
    public float duration; // 持续时间
}

[System.Serializable]
public class LipSyncData
{
    public List<LipSyncFrame> frames = new List<LipSyncFrame>();
}

public class LipSyncController : MonoBehaviour
{
    [Header("VRM Components")]
    public VRMBlendShapeProxy blendShapeProxy;
    
    [Header("Lip Sync Settings")]
    public float analysisWindow = 0.016f; // 16ms窗口
    public float minVolumeThreshold = 0.01f;
    public float maxBlendShapeValue = 1.0f;
    
    [Header("BlendShape Names")]
    public string aBlendShapeName = "A";
    public string iBlendShapeName = "I";
    public string uBlendShapeName = "U";
    public string eBlendShapeName = "E";
    public string oBlendShapeName = "O";
    
    private AudioClip currentAudioClip;
    private bool isPlaying = false;
    
    void Start()
    {
        if (blendShapeProxy == null)
        {
            blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
        }
    }
    
    public async Task<LipSyncData> AnalyzeLipSync(AudioClip audioClip)
    {
        LipSyncData lipSyncData = new LipSyncData();
        
        if (audioClip == null)
        {
            Debug.LogError("AudioClip is null!");
            return lipSyncData;
        }
        
        // 获取音频数据
        float[] samples = new float[audioClip.samples];
        audioClip.GetData(samples, 0);
        
        int samplesPerWindow = Mathf.RoundToInt(audioClip.frequency * analysisWindow);
        int windowCount = samples.Length / samplesPerWindow;
        
        for (int i = 0; i < windowCount; i++)
        {
            LipSyncFrame frame = new LipSyncFrame();
            
            // 计算当前窗口的音频特征
            float[] windowSamples = new float[samplesPerWindow];
            System.Array.Copy(samples, i * samplesPerWindow, windowSamples, 0, samplesPerWindow);
            
            // 分析音频特征
            AnalyzeAudioWindow(windowSamples, frame);
            
            frame.duration = analysisWindow;
            lipSyncData.frames.Add(frame);
        }
        
        return lipSyncData;
    }
    
    private void AnalyzeAudioWindow(float[] samples, LipSyncFrame frame)
    {
        // 计算音量
        float volume = CalculateVolume(samples);
        
        // 计算频谱特征
        float[] spectrum = CalculateSpectrum(samples);
        
        // 基于频谱分析唇形
        AnalyzeSpectrumForLipSync(spectrum, volume, frame);
    }
    
    private float CalculateVolume(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }
    
    private float[] CalculateSpectrum(float[] samples)
    {
        // 简化的FFT计算，实际项目中可以使用更精确的FFT
        int fftSize = 512;
        float[] spectrum = new float[fftSize / 2];
        
        // 这里使用简化的频谱计算
        for (int i = 0; i < spectrum.Length; i++)
        {
            float sum = 0f;
            for (int j = 0; j < samples.Length; j++)
            {
                sum += samples[j] * Mathf.Cos(2f * Mathf.PI * i * j / fftSize);
            }
            spectrum[i] = Mathf.Abs(sum);
        }
        
        return spectrum;
    }
    
    private void AnalyzeSpectrumForLipSync(float[] spectrum, float volume, LipSyncFrame frame)
    {
        // 基于音量调整基础张嘴程度
        float baseOpenness = Mathf.Clamp01(volume / 0.1f) * maxBlendShapeValue;
        
        // 分析不同频率段的特征
        float lowFreq = GetFrequencyRange(spectrum, 0, 50); // 低频
        float midFreq = GetFrequencyRange(spectrum, 50, 200); // 中频
        float highFreq = GetFrequencyRange(spectrum, 200, 800); // 高频
        
        // 根据频率特征调整唇形
        frame.aValue = baseOpenness * (0.5f + lowFreq * 0.5f); // 张嘴
        frame.iValue = baseOpenness * midFreq * 0.3f; // 微笑
        frame.uValue = baseOpenness * (0.2f + highFreq * 0.3f); // 嘟嘴
        frame.eValue = baseOpenness * midFreq * 0.4f; // 咧嘴
        frame.oValue = baseOpenness * (0.3f + lowFreq * 0.4f); // 圆嘴
        
        // 确保值在合理范围内
        frame.aValue = Mathf.Clamp01(frame.aValue);
        frame.iValue = Mathf.Clamp01(frame.iValue);
        frame.uValue = Mathf.Clamp01(frame.uValue);
        frame.eValue = Mathf.Clamp01(frame.eValue);
        frame.oValue = Mathf.Clamp01(frame.oValue);
    }
    
    private float GetFrequencyRange(float[] spectrum, int startIndex, int endIndex)
    {
        float sum = 0f;
        int count = 0;
        
        for (int i = startIndex; i < endIndex && i < spectrum.Length; i++)
        {
            sum += spectrum[i];
            count++;
        }
        
        return count > 0 ? sum / count : 0f;
    }
    
    public IEnumerator PlayWithLipSync(AudioClip audioClip, LipSyncData lipSyncData)
    {
        if (blendShapeProxy == null)
        {
            Debug.LogError("VRMBlendShapeProxy not found!");
            yield break;
        }
        
        currentAudioClip = audioClip;
        isPlaying = true;
        
        // 播放音频
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.clip = audioClip;
        audioSource.Play();
        
        // 同步唇形动画
        foreach (var frame in lipSyncData.frames)
        {
            if (!isPlaying) break;
            
            // 设置BlendShape值
            SetBlendShapeValues(frame);
            
            yield return new WaitForSeconds(frame.duration);
        }
        
        // 重置唇形
        ResetBlendShapes();
        
        isPlaying = false;
        currentAudioClip = null;
    }
    
    private void SetBlendShapeValues(LipSyncFrame frame)
    {
        if (blendShapeProxy == null) return;
        
        // 设置各种唇形BlendShape
        blendShapeProxy.SetValue(aBlendShapeName, frame.aValue);
        blendShapeProxy.SetValue(iBlendShapeName, frame.iValue);
        blendShapeProxy.SetValue(uBlendShapeName, frame.uValue);
        blendShapeProxy.SetValue(eBlendShapeName, frame.eValue);
        blendShapeProxy.SetValue(oBlendShapeName, frame.oValue);
    }
    
    private void ResetBlendShapes()
    {
        if (blendShapeProxy == null) return;
        
        // 重置所有唇形BlendShape
        blendShapeProxy.SetValue(aBlendShapeName, 0f);
        blendShapeProxy.SetValue(iBlendShapeName, 0f);
        blendShapeProxy.SetValue(uBlendShapeName, 0f);
        blendShapeProxy.SetValue(eBlendShapeName, 0f);
        blendShapeProxy.SetValue(oBlendShapeName, 0f);
    }
    
    public void StopLipSync()
    {
        isPlaying = false;
        ResetBlendShapes();
        
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
    
    public bool IsPlaying()
    {
        return isPlaying;
    }
} 