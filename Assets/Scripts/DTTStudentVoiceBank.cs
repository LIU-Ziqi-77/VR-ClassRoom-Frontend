using System.Collections.Generic;
using UnityEngine;

public static class DTTStudentVoiceBank
{
    private const string ResourceRoot = "DTTStudentVoices";
    private static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();
    private static readonly ClassroomItemType[] WrongAnswerCycle =
    {
        ClassroomItemType.Eraser,
        ClassroomItemType.OpenNotebook,
        ClassroomItemType.Pencil,
        ClassroomItemType.Ruler
    };

    public static AudioClip LoadClip(string voiceProfileId, string utteranceKey)
    {
        if (string.IsNullOrEmpty(voiceProfileId) || string.IsNullOrEmpty(utteranceKey))
        {
            return null;
        }

        string resourcePath = $"{ResourceRoot}/{voiceProfileId}/{utteranceKey}";
        AudioClip cached;
        if (ClipCache.TryGetValue(resourcePath, out cached))
        {
            return cached;
        }

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null)
        {
            ClipCache[resourcePath] = clip;
        }

        return clip;
    }

    public static string GetCorrectAnswerKey(ClassroomItemType itemType)
    {
        switch (itemType)
        {
            case ClassroomItemType.Ruler:
                return "ruler_short";
            case ClassroomItemType.Eraser:
                return "eraser_short";
            case ClassroomItemType.Pencil:
                return "pencil_short";
            case ClassroomItemType.OpenNotebook:
                return "notebook_short";
            default:
                return "";
        }
    }

    public static string GetCorrectAnswerText(ClassroomItemType itemType)
    {
        switch (itemType)
        {
            case ClassroomItemType.Ruler:
                return "尺子！";
            case ClassroomItemType.Eraser:
                return "橡皮！";
            case ClassroomItemType.Pencil:
                return "铅笔！";
            case ClassroomItemType.OpenNotebook:
                return "本子！";
            case ClassroomItemType.Cup:
                return "水杯！";
            default:
                return "这个。";
        }
    }

    public static string GetWrongAnswerKey(ClassroomItemType itemType)
    {
        ClassroomItemType wrongItem = GetWrongAnswerItem(itemType);
        return GetCorrectAnswerKey(wrongItem);
    }

    public static string GetWrongAnswerText(ClassroomItemType itemType)
    {
        return GetCorrectAnswerText(GetWrongAnswerItem(itemType));
    }

    private static ClassroomItemType GetWrongAnswerItem(ClassroomItemType itemType)
    {
        for (int i = 0; i < WrongAnswerCycle.Length; i++)
        {
            if (WrongAnswerCycle[i] != itemType)
            {
                return WrongAnswerCycle[i];
            }
        }

        return ClassroomItemType.Eraser;
    }

    public static string GetFallbackText(string utteranceKey)
    {
        switch (utteranceKey)
        {
            case "huh_what":
                return "啊？什么？";
            case "i_dont_know":
                return "我不知道。";
            case "i_cannot":
                return "老师，我不会。";
            case "maybe_this":
                return "是这个吗？";
            case "okay":
                return "好。";
            case "okay_teacher":
                return "好的，老师。";
            default:
                return utteranceKey;
        }
    }
}
