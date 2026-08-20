#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>이전 사운드 오브젝트를 씬에서 정리하는 호환용 도구.</summary>
    public static class GameSfxPlayerSceneInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";

        [MenuItem("Tools/Defence/Remove Legacy Game SFX Player")]
        public static void RemoveLegacyPlayer()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = null;
            foreach (var candidate in scene.GetRootGameObjects())
            {
                if (candidate == null || candidate.name != "GameSfxPlayer")
                    continue;

                root = candidate;
                break;
            }
            if (root != null)
                Object.DestroyImmediate(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Legacy GameSfxPlayer removed from SampleScene.");
        }
    }
}
#endif
