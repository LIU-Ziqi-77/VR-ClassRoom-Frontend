using UnityEditor;

public class DTTLeaveSeatFbxImporter : AssetPostprocessor
{
    const string LayingClipPath = "Assets/Resources/DTTAnimationClips/Laying Shaking Head.fbx";
    const string GettingUpClipPath = "Assets/Resources/DTTAnimationClips/Getting Up.fbx";

    void OnPreprocessModel()
    {
        if (assetPath != LayingClipPath && assetPath != GettingUpClipPath)
            return;

        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null)
            return;

        bool isLayingClip = assetPath == LayingClipPath;
        string clipName = isLayingClip ? "Laying Shaking Head" : "Getting Up";

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.resampleCurves = true;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return;

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = i == 0 ? clipName : clips[i].name;
            clips[i].loopTime = isLayingClip;
            clips[i].loopPose = isLayingClip;
            clips[i].lockRootPositionXZ = true;
            clips[i].lockRootHeightY = true;
            clips[i].lockRootRotation = true;
        }

        importer.clipAnimations = clips;
    }

    public static void ReimportLeaveSeatClipsBatch()
    {
        AssetDatabase.ImportAsset(LayingClipPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(GettingUpClipPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }
}
