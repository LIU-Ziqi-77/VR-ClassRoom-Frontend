#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps Android/Quest builds on the Input System Package setting.
/// Android does not support PlayerSettings.activeInputHandling = Both.
/// </summary>
[InitializeOnLoad]
public class AndroidInputHandlingGuard : IPreprocessBuildWithReport
{
    private const int InputSystemPackage = 1;

    static AndroidInputHandlingGuard()
    {
        EditorApplication.delayCall += EnsureAndroidSafeInputHandling;
    }

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
        {
            EnsureAndroidSafeInputHandling();
        }
    }

    [MenuItem("Tools/VR Classroom/Fix Android Input Handling")]
    public static void EnsureAndroidSafeInputHandling()
    {
        if (TrySetViaPlayerSettings())
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidInputHandlingGuard] Active Input Handling set to Input System Package.");
            return;
        }

        if (TryPatchProjectSettingsFile())
        {
            AssetDatabase.Refresh();
            Debug.LogWarning("[AndroidInputHandlingGuard] Patched ProjectSettings.asset directly.");
        }
    }

    private static bool TrySetViaPlayerSettings()
    {
        PropertyInfo property = typeof(PlayerSettings).GetProperty(
            "activeInputHandling",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        if (property == null || !property.CanWrite)
        {
            return false;
        }

        object desired = Enum.ToObject(property.PropertyType, InputSystemPackage);
        object current = property.GetValue(null);
        if (!Equals(current, desired))
        {
            property.SetValue(null, desired);
        }

        return true;
    }

    private static bool TryPatchProjectSettingsFile()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot)) return false;

        string path = Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset");
        if (!File.Exists(path)) return false;

        string text = File.ReadAllText(path);
        string patched = text
            .Replace("activeInputHandler: 2", "activeInputHandler: 1")
            .Replace("activeInputHandler: 0", "activeInputHandler: 1");

        if (patched == text) return false;

        File.WriteAllText(path, patched);
        return true;
    }
}
#endif
