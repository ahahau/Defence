using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.UI
{
    /// <summary>
    /// Unity의 전역 Find API 없이 로드된 씬 루트와 명시된 부모 아래의 UI를 조회한다.
    /// </summary>
    internal static class SceneUiRegistry
    {
        public static IEnumerable<T> EnumerateLoaded<T>(bool includeInactive = true) where T : Component
        {
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;

                    foreach (var component in root.GetComponentsInChildren<T>(includeInactive))
                    {
                        if (component != null)
                            yield return component;
                    }
                }
            }
        }

        public static T GetDirectChild<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
                return null;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == childName)
                    return child.GetComponent<T>();
            }

            return null;
        }
    }
}
