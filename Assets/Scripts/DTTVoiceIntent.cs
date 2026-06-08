using System;

[Serializable]
public class DTTVoiceIntentMessage
{
    public string intent;
    public string text;
    public string student_id;
    public string student_name;
    public double timestamp;
}

public enum DTTTeacherIntent
{
    Unknown,
    WhatIsThis,
    PositiveReinforcement,
    RetryOrCorrection,
    HalfPrompt,
    FullPrompt,
    ClapHands,
    TouchNose,
    SelectStudent
}

public static class DTTTeacherIntentParser
{
    public static DTTTeacherIntent Parse(string rawIntent)
    {
        if (string.IsNullOrWhiteSpace(rawIntent)) return DTTTeacherIntent.Unknown;

        string value = rawIntent.Trim();
        if (value.StartsWith("SelectStudent", StringComparison.OrdinalIgnoreCase))
        {
            return DTTTeacherIntent.SelectStudent;
        }

        DTTTeacherIntent parsed;
        return Enum.TryParse(value, true, out parsed) ? parsed : DTTTeacherIntent.Unknown;
    }
}
