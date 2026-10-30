using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>편집 씬은 그대로 두고, 스테이지가 활성 씬일 때만 단독 시작 씬으로 지정한다.</summary>
public static class StageEditorPlayEntry
{
    private const string StagePath = "Assets/00.Scenes/StageDefense.unity";
    private const string OwnershipKey = "Defence.StageEditorPlayEntry.OwnsStartScene";

    [InitializeOnLoadMethod]
    private static void Register()
    {
        Unregister();
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorSceneManager.sceneClosed += HandleSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.projectChanged += Refresh;
        EditorApplication.quitting += RestoreOwnedStartScene;
        AssemblyReloadEvents.beforeAssemblyReload += Unregister;
        EditorApplication.delayCall += Refresh;
    }

    private static void Unregister()
    {
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneClosed -= HandleSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.projectChanged -= Refresh;
        EditorApplication.quitting -= RestoreOwnedStartScene;
        AssemblyReloadEvents.beforeAssemblyReload -= Unregister;
        EditorApplication.delayCall -= Refresh;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode) => Refresh();
    private static void HandleSceneClosed(Scene scene) => Refresh();
    private static void HandleActiveSceneChanged(Scene previous, Scene next) => Refresh();

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            Refresh();
    }

    private static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        // 다른 씬과 함께 열어 두고 그쪽을 활성화했으면 그 씬을 플레이할 수 있게 둔다.
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded || active.path != StagePath)
        {
            RestoreOwnedStartScene();
            return;
        }
        SceneAsset configured = EditorSceneManager.playModeStartScene;
        if (configured != null)
        {
            // 다른 도구/사용자가 지정한 시작 씬의 소유권을 가져오지 않는다.
            if (AssetDatabase.GetAssetPath(configured) != StagePath)
                SessionState.SetBool(OwnershipKey, false);
            return;
        }
        SceneAsset start = AssetDatabase.LoadAssetAtPath<SceneAsset>(StagePath);
        if (start == null)
            return;
        EditorSceneManager.playModeStartScene = start;
        SessionState.SetBool(OwnershipKey, true);
    }

    private static void RestoreOwnedStartScene()
    {
        if (!SessionState.GetBool(OwnershipKey, false))
            return;
        if (EditorSceneManager.playModeStartScene != null
            && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == StagePath)
            EditorSceneManager.playModeStartScene = null;
        SessionState.SetBool(OwnershipKey, false);
    }
}
