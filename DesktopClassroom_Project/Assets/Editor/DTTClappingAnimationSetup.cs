using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DTTClappingAnimationSetup
{
    private const string TargetScenePath = "Assets/Scenes/HighSchoolClassroom_Demo.unity";
    private const string ClappingFbxPath = "Assets/Animations/DTT/sitting_clapping_1.fbx";

    [MenuItem("Tools/DTT/Setup Clapping Animation")]
    public static void SetupClappingAnimation()
    {
        ConfigureClappingImporter();
        AnimationClip clip = LoadClappingClip();
        if (clip == null)
        {
            throw new InvalidOperationException($"No AnimationClip found in {ClappingFbxPath}");
        }

        AssignClipToScene(clip);
    }

    public static void SetupClappingAnimationBatch()
    {
        try
        {
            SetupClappingAnimation();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigureClappingImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ClappingFbxPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"ModelImporter not found for {ClappingFbxPath}");
        }

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.resampleCurves = true;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = i == 0 ? "sitting_clapping_1" : clips[i].name;
                clips[i].loopTime = true;
                clips[i].loopPose = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootRotation = true;
            }

            importer.clipAnimations = clips;
        }

        AssetDatabase.ImportAsset(ClappingFbxPath, ImportAssetOptions.ForceUpdate);
    }

    private static AnimationClip LoadClappingClip()
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(ClappingFbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssignClipToScene(AnimationClip clip)
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        BehaviorDemoSetup[] setups = UnityEngine.Object.FindObjectsOfType<BehaviorDemoSetup>(true);
        foreach (BehaviorDemoSetup setup in setups)
        {
            setup.clappingClip = clip;
            EditorUtility.SetDirty(setup);
        }

        BehaviorDemoController[] controllers = UnityEngine.Object.FindObjectsOfType<BehaviorDemoController>(true);
        foreach (BehaviorDemoController controller in controllers)
        {
            controller.clappingClip = clip;
            controller.clapDuration = 3f;
            EditorUtility.SetDirty(controller);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"DTT clapping animation assigned: {clip.name} from {ClappingFbxPath}. Setups: {setups.Length}, controllers: {controllers.Length}");
    }
}
