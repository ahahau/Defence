#if UNITY_EDITOR
using _01.Code.Manager;
using _01.Code.Persistence;
using _01.Code.Persistence.Agents;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 주둔 마력을 저장 명단에 올린다. 메뉴에서 한 번만 누르면 된다.
    ///
    /// 왜 필요한가 — 마력은 두 방향으로 새고 있었다.
    /// 상한은 마을을 완전히 장악하면 오르는데 저장되지 않아 불러오면 보상이 사라지고,
    /// 사용량은 복원이 유닛을 직접 인스턴스화하느라 마력 시스템을 거치지 않아 0으로 남는다
    /// (배치된 부하가 그대로인데 마력이 비어 있으니, 저장하고 불러오면 공짜로 더 세울 수 있다).
    ///
    /// 여러 번 눌러도 안전하다 — 이미 붙어 있으면 그대로 두고 명단에도 한 번만 들어간다.
    /// </summary>
    public static class MagicSaveAgentInstaller
    {
        [MenuItem("Tools/Defence/주둔 마력 저장 배선")]
        public static void Install()
        {
            var registry = Object.FindAnyObjectByType<SaveAgentRegistry>(FindObjectsInactive.Include);
            if (registry == null)
            {
                Debug.LogError($"{nameof(SaveAgentRegistry)}를 찾지 못했습니다. 저장 명단이 있는 씬을 열고 다시 실행하세요.");
                return;
            }

            if (Object.FindAnyObjectByType<MagicManager>(FindObjectsInactive.Include) == null)
                Debug.LogWarning($"{nameof(MagicManager)}가 씬에 없습니다. 에이전트는 붙지만 저장할 값이 없습니다.");

            var host = registry.gameObject;
            var agent = host.GetComponent<MagicSaveAgent>();
            if (agent == null)
            {
                agent = Undo.AddComponent<MagicSaveAgent>(host);
                Debug.Log($"{nameof(MagicSaveAgent)}를 {host.name}에 붙였습니다.");
            }

            var serialized = new SerializedObject(registry);
            var list = serialized.FindProperty("saveAgents");

            var alreadyListed = false;
            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == agent)
                    alreadyListed = true;
            }

            if (!alreadyListed)
            {
                // 복원 순서는 의존 관계다. 마력은 아무것도 필요로 하지 않으므로 끝에 붙여도 안전하다.
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = agent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("저장 명단에 magic.state를 등록했습니다.");
            }

            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(host.scene);
            EditorSceneManager.SaveScene(host.scene);

            var check = new SerializedObject(registry).FindProperty("saveAgents");
            var names = new System.Text.StringBuilder("저장 명단 " + check.arraySize + "개: ");
            for (var i = 0; i < check.arraySize; i++)
            {
                var value = check.GetArrayElementAtIndex(i).objectReferenceValue;
                names.Append(value != null ? value.GetType().Name : "null").Append(i < check.arraySize - 1 ? ", " : "");
            }

            Debug.Log(names.ToString());
        }
    }
}
#endif
