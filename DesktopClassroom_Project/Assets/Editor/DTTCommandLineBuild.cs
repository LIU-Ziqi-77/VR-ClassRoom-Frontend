#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DTTCommandLineBuild
{
    private const string DefaultApkPath = "Builds/Quest/DTTWorkflow.apk";

    public static void BuildQuestApk()
    {
        try
        {
            string outputPath = GetArgumentValue("-dttBuildPath", DefaultApkPath);
            BuildQuestApk(outputPath);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildQuestApk(string outputPath)
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.forceInternetPermission = true;

        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledBuildScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Quest APK build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
        }

        Debug.Log($"[DTTCommandLineBuild] Quest APK built: {outputPath}");
    }

    private static string[] GetEnabledBuildScenes()
    {
        List<string> scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0)
        {
            throw new InvalidOperationException("No enabled scenes are configured in EditorBuildSettings.");
        }

        return scenes.ToArray();
    }

    private static string GetArgumentValue(string name, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return defaultValue;
    }
}
#endif
