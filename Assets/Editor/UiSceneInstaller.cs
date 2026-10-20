using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// UI 프리팹을 SampleScene의 <c>======UI===========</c> 아래에 놓는다. 같은 화면이 이미 있으면 그 프리팹 인스턴스만 바꾼다.
/// 씬 YAML을 손으로 고치지 않으려고 둔 공용 설치기다.
/// </summary>
public static class UiSceneInstaller
{
    public const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
    private const string UiRootName = "======UI===========";

    /// <param name="marker">이 화면을 알아보는 컴포넌트 형식. 있으면 그 프리팹 인스턴스를 지우고 새로 놓는다.</param>
    /// <param name="wire">놓은 인스턴스에 씬 참조를 잇는다.</param>
    public static GameObject Place(string prefabPath, Type marker, Action<GameObject> wire)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        foreach (var existing in Object.FindObjectsByType(marker, FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var component = existing as Component;
            if (component == null)
                continue;

            // 이 프리팹으로 만든 인스턴스만 지운다. 그 위(UI 루트 등)는 건드리지 않는다.
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject);
            if (instanceRoot != null && PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) == prefab)
                Object.DestroyImmediate(instanceRoot);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var uiRoot = GameObject.Find(UiRootName);
        if (uiRoot != null)
            instance.transform.SetParent(uiRoot.transform, false);

        wire?.Invoke(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return instance;
    }
}
