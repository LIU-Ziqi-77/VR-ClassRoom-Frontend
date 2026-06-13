using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Play mode pointed at the runnable classroom demo instead of Unity's
/// default untitled scene.
/// </summary>
[InitializeOnLoad]
public static class VRClassroomPlayModeStartScene
{
    const string DemoScenePath = "Assets/Scenes/HighSchoolClassroom_Demo.unity";

    static VRClassroomPlayModeStartScene()
    {
        EditorApplication.delayCall += EnsurePlayModeStartScene;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/VR Classroom/Open Demo Scene")]
    public static void OpenDemoScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[VRClassroom] Stop Play mode before opening the demo scene.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        EnsurePlayModeStartScene();
    }

    [MenuItem("Tools/VR Classroom/Use Demo Scene When Pressing Play")]
    public static void EnsurePlayModeStartScene()
    {
        SceneAsset demoScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
        if (demoScene == null)
        {
            Debug.LogWarning($"[VRClassroom] Demo scene not found: {DemoScenePath}");
            return;
        }

        if (EditorSceneManager.playModeStartScene != demoScene)
        {
            EditorSceneManager.playModeStartScene = demoScene;
            Debug.Log($"[VRClassroom] Play mode start scene set to {DemoScenePath}");
        }
    }

    [MenuItem("Tools/VR Classroom/Use Demo Scene When Pressing Play", true)]
    static bool ValidateEnsurePlayModeStartScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/VR Classroom/Clear Play Mode Start Scene")]
    public static void ClearPlayModeStartScene()
    {
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[VRClassroom] Play mode start scene cleared.");
    }

    [MenuItem("Tools/VR Classroom/Clear Play Mode Start Scene", true)]
    static bool ValidateClearPlayModeStartScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               EditorSceneManager.playModeStartScene != null;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ResumeIfPaused();
            EditorApplication.delayCall += ResumeIfPaused;
        }
    }

    static void ResumeIfPaused()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused) return;

        EditorApplication.isPaused = false;
        Debug.Log("[VRClassroom] Editor pause was enabled; Play mode has been resumed.");
    }
}
