#if UNITY_EDITOR
using System.Collections.Generic;
using _01.Code.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace _01.Code.Editor
{
    public static class GameSfxPlayerSceneInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string NodeEventChannelGuid = "f1b1e574478592a46b08a62e4c3db082";
        private const string CostEventChannelGuid = "734ca3593b5d2884dae267b0e3e601be";
        private const string WaveEventChannelGuid = "dd03e0a5a2140e441aba26879f409cee";

        [MenuItem("Tools/Defence/Install Game SFX Player")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GetOrCreateRoot("GameSfxPlayer");
            var player = GetOrAdd<GameSfxPlayer>(root);
            var source = GetOrAdd<AudioSource>(root);

            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var serialized = new SerializedObject(player);
            SetObject(serialized, "nodeEventChannel", AssetDatabase.GUIDToAssetPath(NodeEventChannelGuid));
            SetObject(serialized, "costEventChannel", AssetDatabase.GUIDToAssetPath(CostEventChannelGuid));
            SetObject(serialized, "waveEventChannel", AssetDatabase.GUIDToAssetPath(WaveEventChannelGuid));

            SetClips(serialized, "uiClickClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Navigate_UI_Short_Click_01.wav",
                "Assets/Action_RPG_SFX/UI/Navigate_UI_Short_Click_02.wav",
                "Assets/Action_RPG_SFX/UI/Navigate_UI_Short_Click_03.wav",
            });
            SetClips(serialized, "uiConfirmClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Confirm_UI_Impact_01.wav",
                "Assets/Action_RPG_SFX/UI/Confirm_UI_Impact_02.wav",
                "Assets/Action_RPG_SFX/UI/Confirm_UI_Impact_03.wav",
            });
            SetClips(serialized, "uiOpenClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Inventory_UI_Open_Impact_01.wav",
                "Assets/Action_RPG_SFX/UI/Inventory_UI_Open_Impact_02.wav",
                "Assets/Action_RPG_SFX/UI/Inventory_UI_Open_Impact_03.wav",
            });
            SetClips(serialized, "uiRewardClips", new[]
            {
                "Assets/Action_RPG_SFX/Effects/Level Up_Rise_Effect_01 .wav",
                "Assets/Action_RPG_SFX/Effects/Level Up_Rise_Effect_02.wav",
                "Assets/Action_RPG_SFX/Effects/Level Up_Rise_Effect_03.wav",
            });
            // 건물 설치: UI 라이즈 대신 돌을 놓는 듯한 무게감 있는 소리.
            SetClips(serialized, "buildInstallClips", new[]
            {
                "Assets/Action_RPG_SFX/Interactive Object/Trigger_Stone_Quick_01.wav",
                "Assets/Action_RPG_SFX/Interactive Object/Trigger_Stone_Quick_02.wav",
                "Assets/Action_RPG_SFX/Interactive Object/Trigger_Stone_Quick_03.wav",
            });
            SetClips(serialized, "unitPlaceClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Select_UI_Bell_Bright_01.wav",
                "Assets/Action_RPG_SFX/UI/Select_UI_Bell_Bright_02.wav",
                "Assets/Action_RPG_SFX/UI/Select_UI_Bell_Bright_03.wav",
            });
            // 웨이브 시작: UI 선택음 대신 석문이 열리는 소리(포탈에서 침공 시작).
            SetClips(serialized, "waveStartClips", new[]
            {
                "Assets/Action_RPG_SFX/Interactive Object/Gate_Stone_Long_Open_01.wav",
                "Assets/Action_RPG_SFX/Interactive Object/Gate_Stone_Long_Open_02.wav",
                "Assets/Action_RPG_SFX/Interactive Object/Gate_Stone_Long_Open_03.wav",
            });
            SetClips(serialized, "waveClearClips", new[]
            {
                "Assets/Action_RPG_SFX/Effects/Level Up 1_Rise_Effect_01 .wav",
                "Assets/Action_RPG_SFX/Effects/Level Up 1_Rise_Effect_02.wav",
                "Assets/Action_RPG_SFX/Effects/Level Up 1_Rise_Effect_03.wav",
            });
            // 근접/탱커: 단검 휘두르기 대신 묵직한 검격.
            SetClips(serialized, "attackClips", new[]
            {
                "Assets/Action_RPG_SFX/Attack/Sword Swing_Knight_Hard_01.wav",
                "Assets/Action_RPG_SFX/Attack/Sword Swing_Knight_Hard_02.wav",
                "Assets/Action_RPG_SFX/Attack/Sword Swing_Knight_Hard_03.wav",
            });
            // 원거리: 활 사격음.
            SetClips(serialized, "attackBowClips", new[]
            {
                "Assets/Action_RPG_SFX/Attack/Shooting_Archer_Arrow_Bow_01.wav",
                "Assets/Action_RPG_SFX/Attack/Shooting_Archer_Arrow_Bow_02.wav",
                "Assets/Action_RPG_SFX/Attack/Shooting_Archer_Arrow_Bow_03.wav",
            });
            // 지원(마법): 마법 화살.
            SetClips(serialized, "attackMagicClips", new[]
            {
                "Assets/Action_RPG_SFX/Attack/Fire Arrow_Attack_Sorcerer_01.wav",
                "Assets/Action_RPG_SFX/Attack/Fire Arrow_Attack_Sorcerer_02.wav",
                "Assets/Action_RPG_SFX/Attack/Fire Arrow_Attack_Sorcerer_03.wav",
            });
            SetClips(serialized, "hitClips", new[]
            {
                "Assets/Action_RPG_SFX/Combat/Combat_Hit_Cut_01.wav",
                "Assets/Action_RPG_SFX/Combat/Combat_Hit_Cut_02.wav",
                "Assets/Action_RPG_SFX/Combat/Combat_Hit_Cut_03.wav",
            });
            // 회피: 더 짧고 경쾌한 휘릭(다대다에서 소리 겹침 완화).
            SetClips(serialized, "dodgeClips", new[]
            {
                "Assets/Action_RPG_SFX/Movement/Dodge_Fast_Short_Whoosh_01.wav",
                "Assets/Action_RPG_SFX/Movement/Dodge_Fast_Short_Whoosh_02.wav",
                "Assets/Action_RPG_SFX/Movement/Dodge_Fast_Short_Whoosh_03.wav",
            });
            // 트랩: 뼈 부러짐 대신 팩의 전용 가시 트랩 소리.
            SetClips(serialized, "trapClips", new[]
            {
                "Assets/Action_RPG_SFX/Interactive Object/Trap_Sharp_Spike_01.wav",
                "Assets/Action_RPG_SFX/Interactive Object/Trap_Sharp_Spike_02.wav",
                "Assets/Action_RPG_SFX/Interactive Object/Trap_Sharp_Spike_03.wav",
            });
            // 스킬 시전: 무기 스킬 휘두름.
            SetClips(serialized, "skillCastClips", new[]
            {
                "Assets/Action_RPG_SFX/Skill/Swirl Attack_Skill_Weapon_01.wav",
                "Assets/Action_RPG_SFX/Skill/Swirl Attack_Skill_Weapon_02.wav",
                "Assets/Action_RPG_SFX/Skill/Swirl Attack_Skill_Weapon_03.wav",
            });
            // 폭발(메테오 장판 등).
            SetClips(serialized, "explosionClips", new[]
            {
                "Assets/Action_RPG_SFX/Effects/Explosive_Buff_Temple_Effect_01.wav",
                "Assets/Action_RPG_SFX/Effects/Explosive_Buff_Temple_Effect_02.wav",
                "Assets/Action_RPG_SFX/Effects/Explosive_Buff_Temple_Effect_03.wav",
            });

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Game SFX player installed.");
        }

        private static GameObject GetOrCreateRoot(string objectName)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == objectName)
                    return root;
            }

            return new GameObject(objectName);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            if (target.TryGetComponent<T>(out var component))
                return component;

            return target.AddComponent<T>();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            SetObject(serialized, propertyName, AssetDatabase.LoadAssetAtPath<Object>(assetPath));
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetClips(SerializedObject serialized, string propertyName, IReadOnlyList<string> paths)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return;

            property.arraySize = paths.Count;
            for (var i = 0; i < paths.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
        }
    }
}
#endif
