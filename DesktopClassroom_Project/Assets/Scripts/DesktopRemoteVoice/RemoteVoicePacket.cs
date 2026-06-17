using System;

[Serializable]
public class RemoteVoicePacket
{
    public string type;
    public string studentId;
    public int studentIndex;
    public int sampleRate;
    public int channels;
    public int sequence;
    public string payloadBase64;
    public string behavior;
    public string utteranceKey;
    public string voiceProfileId;
    public string text;
    public float duration;
    public float gain = 1f;
    public double timestamp;
}

public static class RemoteVoicePacketTypes
{
    public const string VoiceStart = "voice_start";
    public const string VoiceChunk = "voice_chunk";
    public const string VoiceStop = "voice_stop";
    public const string PresetLine = "preset_line";
    public const string Behavior = "behavior";
    public const string SelectStudent = "select_student";
    public const string Refresh = "refresh";
}
