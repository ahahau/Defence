using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 게임 화면 후처리(블룸)를 만든다. 볼륨 설정 묶음을 만들어 씬의 전역 볼륨에 걸고 메인 카메라 후처리를 켠다.
/// 블룸은 횃불·스킬·함정 불꽃처럼 밝은 픽셀만 은은하게 번지게 약하게 둔다 — 세면 도트가 뭉개진다.
/// Unity CLI: <c>PostProcessingBuilder.BuildAll()</c> (SampleScene을 연 상태).
/// </summary>
public static class PostProcessingBuilder
{
    public const string ProfilePath = "Assets/05.Materials/PostProcessing/GameVolumeProfile.asset";
    public const float BloomThreshold = 0.85f;
    public const float BloomIntensity = 0.5f;
    public const float BloomScatter = 0.6f;

    [MenuItem("Tools/Dungeon Keeper/Build Post Processing")]
    public static void BuildAll()
    {
        var profile = EnsureProfile();

        var volume = Object.FindAnyObjectByType<Volume>(FindObjectsInactive.Include);
        if (volume == null)
        {
            volume = new GameObject("Global Volume").AddComponent<Volume>();
            Undo.RegisterCreatedObjectUndo(volume.gameObject, "Create Global Volume");
        }

        volume.isGlobal = true;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        var camera = Camera.main;
        if (camera != null)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
        }
        else
        {
            Debug.LogError($"{nameof(PostProcessingBuilder)}: 메인 카메라를 찾지 못해 후처리를 켜지 못했습니다.");
        }

        EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
    }

    private static VolumeProfile EnsureProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            UiKit.EnsureFolder("Assets/05.Materials/PostProcessing");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!profile.TryGet<Bloom>(out var bloom))
            bloom = profile.Add<Bloom>(true);

        bloom.active = true;
        bloom.threshold.Override(BloomThreshold);
        bloom.intensity.Override(BloomIntensity);
        bloom.scatter.Override(BloomScatter);
        bloom.highQualityFiltering.Override(false);

        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }
}
