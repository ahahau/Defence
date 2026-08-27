#if UNITY_EDITOR
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Editor
{
    /// <summary>
    /// 정기 이벤트를 늘려 20일 한 판이 매번 같은 사건으로 흘러가지 않게 한다.
    /// 선택지와 효과를 손으로 채우면 어떤 수치가 왜 그런지 남지 않으므로 코드로 남긴다.
    /// </summary>
    public static class DayEventInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string SequenceFolder = "Assets/03.SO/Dialogue/Sequences";
        private const string ActionFolder = "Assets/03.SO/Dialogue/Actions";

        /// <summary>한 선택지가 주는 결과. 금화와 민심만으로도 대부분의 사건을 표현할 수 있다.</summary>
        private readonly struct Outcome
        {
            public Outcome(string label, int gold, int morale)
            {
                Label = label;
                Gold = gold;
                Morale = morale;
            }

            public string Label { get; }
            public int Gold { get; }
            public int Morale { get; }
        }

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var runner = FirstComponent<DialogueRunner>(scene);
            if (runner == null)
                throw new System.InvalidOperationException("씬에서 DialogueRunner를 찾지 못했습니다.");

            var costChannel = ReadCostChannel(runner);
            var created = new List<DialogueSequenceSO>
            {
                Build(costChannel, "DayEvent_TunnelCollapse", "무너진 갱도",
                    "굴착 담당",
                    "던전 아래 갱도가 내려앉았습니다. 메우자니 인력이 들고, 두자니 침입로가 하나 늘어납니다.",
                    new Outcome("인부를 붙여 메운다", -45, 6),
                    new Outcome("그대로 둔다", 0, -8)),

                Build(costChannel, "DayEvent_RivalWarlord", "이웃 군주의 전갈",
                    "검은 뿔 사절",
                    "이웃 군주가 상납을 요구합니다. 보내면 조용해지고, 거절하면 우리 부하들이 우쭐해집니다.",
                    new Outcome("요구대로 보낸다", -60, 4),
                    new Outcome("사절을 돌려보낸다", 0, 10)),

                Build(costChannel, "DayEvent_CursedRelic", "파묻힌 유물",
                    "발굴반",
                    "봉인된 유물을 파냈습니다. 팔면 값이 나가지만, 부하들은 불길하다며 꺼립니다.",
                    new Outcome("상인에게 넘긴다", 70, -10),
                    new Outcome("봉인해 묻어둔다", -15, 8)),

                Build(costChannel, "DayEvent_DeserterHunt", "도망친 부하",
                    "감독관",
                    "부하 하나가 계약을 어기고 달아났습니다. 본보기로 잡아올지, 없던 일로 할지 정해야 합니다.",
                    new Outcome("추격대를 보낸다", -30, -6),
                    new Outcome("보내준다", 0, 6)),

                Build(costChannel, "DayEvent_BlackMarket", "야시장 제안",
                    "복면 중개인",
                    "장물아비가 은밀한 거래를 제안합니다. 이문은 크지만 소문이 돌면 민심이 상합니다.",
                    new Outcome("거래한다", 85, -12),
                    new Outcome("거절한다", 0, 4))
            };

            RegisterInRunner(runner, created);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Added {created.Count} day events.", runner);
        }

        private static DialogueSequenceSO Build(
            GameEventChannelSO costChannel, string fileName, string title,
            string speaker, string body, Outcome first, Outcome second)
        {
            var path = $"{SequenceFolder}/{fileName}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(path);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<DialogueSequenceSO>();
                AssetDatabase.CreateAsset(sequence, path);
            }

            var serialized = new SerializedObject(sequence);
            serialized.FindProperty("displayTitle").stringValue = title;

            var lines = serialized.FindProperty("lines");
            lines.arraySize = 1;
            var line = lines.GetArrayElementAtIndex(0);
            line.FindPropertyRelative("speakerName").stringValue = speaker;
            line.FindPropertyRelative("text").stringValue = body;
            line.FindPropertyRelative("enterActions").arraySize = 0;

            var choices = line.FindPropertyRelative("choices");
            choices.arraySize = 2;
            FillChoice(choices.GetArrayElementAtIndex(0), costChannel, first);
            FillChoice(choices.GetArrayElementAtIndex(1), costChannel, second);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static void FillChoice(SerializedProperty choice, GameEventChannelSO costChannel, Outcome outcome)
        {
            choice.FindPropertyRelative("text").stringValue = outcome.Label;
            choice.FindPropertyRelative("effectSummary").stringValue = BuildSummary(outcome);
            choice.FindPropertyRelative("nextSequence").objectReferenceValue = null;
            choice.FindPropertyRelative("nextLineIndex").intValue = -1;
            choice.FindPropertyRelative("routes").arraySize = 0;

            var actions = choice.FindPropertyRelative("actions");
            var built = new List<Object>();
            if (outcome.Gold != 0)
                built.Add(EnsureGoldAction(costChannel, outcome.Gold));
            if (outcome.Morale != 0)
                built.Add(EnsureMoraleAction(outcome.Morale));

            actions.arraySize = built.Count;
            for (var i = 0; i < built.Count; i++)
                actions.GetArrayElementAtIndex(i).objectReferenceValue = built[i];
        }

        /// <summary>선택지에 결과를 미리 적어 두지 않으면 눈 감고 고르는 것과 같다.</summary>
        private static string BuildSummary(Outcome outcome)
        {
            var parts = new List<string>();
            if (outcome.Gold != 0)
                parts.Add($"골드 {(outcome.Gold > 0 ? "+" : "")}{outcome.Gold}");
            if (outcome.Morale != 0)
                parts.Add($"민심 {(outcome.Morale > 0 ? "+" : "")}{outcome.Morale}");
            return parts.Count == 0 ? "변화 없음" : string.Join(" / ", parts);
        }

        /// <summary>같은 액수의 액션 에셋을 매번 새로 만들지 않고 재사용한다.</summary>
        private static Object EnsureGoldAction(GameEventChannelSO costChannel, int amount)
        {
            var name = amount > 0 ? $"GoldPlus{amount}DialogueAction" : $"GoldMinus{-amount}DialogueAction";
            return EnsureAction<GoldChangeDialogueActionSO>(name, "costEventChannel", costChannel, amount);
        }

        private static Object EnsureMoraleAction(int amount)
        {
            var name = amount > 0 ? $"MoralePlus{amount}DialogueAction" : $"MoraleMinus{-amount}DialogueAction";
            // 민심은 운영 채널이 아니라 관리 채널로 간다. 기존 액션에서 어느 채널인지 그대로 읽어 온다.
            return EnsureAction<MoraleChangeDialogueActionSO>(
                name, "managementEventChannel", BorrowChannel<MoraleChangeDialogueActionSO>("managementEventChannel"), amount);
        }

        private static Object EnsureAction<T>(
            string assetName, string channelField, Object channel, int amount)
            where T : DialogueActionSO
        {
            var path = $"{ActionFolder}/{assetName}.asset";
            var action = AssetDatabase.LoadAssetAtPath<T>(path);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(action, path);
            }

            var serialized = new SerializedObject(action);
            serialized.FindProperty("amount").intValue = amount;

            var channelProperty = serialized.FindProperty(channelField);
            if (channelProperty != null && channelProperty.objectReferenceValue == null)
                channelProperty.objectReferenceValue = channel;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            return action;
        }

        /// <summary>같은 종류의 기존 액션이 쓰는 채널을 빌려온다. 채널을 새로 만들면 아무도 듣지 않는다.</summary>
        private static Object BorrowChannel<T>(string channelField) where T : DialogueActionSO
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { ActionFolder }))
            {
                var existing = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing == null)
                    continue;

                var channel = new SerializedObject(existing).FindProperty(channelField)?.objectReferenceValue;
                if (channel != null)
                    return channel;
            }

            return null;
        }

        private static void RegisterInRunner(DialogueRunner runner, List<DialogueSequenceSO> created)
        {
            var serialized = new SerializedObject(runner);
            var list = serialized.FindProperty("scheduledEventSequences");

            var existing = new List<Object>();
            for (var i = 0; i < list.arraySize; i++)
            {
                var value = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null)
                    existing.Add(value);
            }

            foreach (var sequence in created)
            {
                if (!existing.Contains(sequence))
                    existing.Add(sequence);
            }

            list.arraySize = existing.Count;
            for (var i = 0; i < existing.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = existing[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runner);
        }

        private static GameEventChannelSO ReadCostChannel(DialogueRunner runner)
        {
            var serialized = new SerializedObject(runner);
            return serialized.FindProperty("costEventChannel").objectReferenceValue as GameEventChannelSO;
        }

        private static T FirstComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }
    }
}
#endif
