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
            SetClips(serialized, "buildInstallClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Stage Selected_Long_Rise_01.wav",
                "Assets/Action_RPG_SFX/UI/Stage Selected_Long_Rise_02.wav",
                "Assets/Action_RPG_SFX/UI/Stage Selected_Long_Rise_03.wav",
            });
            SetClips(serialized, "unitPlaceClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Select_UI_Bell_Bright_01.wav",
                "Assets/Action_RPG_SFX/UI/Select_UI_Bell_Bright_02.wav",
                "Assets/Action_RPG_SFX/UI/Select_UI_Bell_Bright_03.wav",
            });
            SetClips(serialized, "waveStartClips", new[]
            {
                "Assets/Action_RPG_SFX/UI/Stage Selected 1_Long_Ap_01.wav",
                "Assets/Action_RPG_SFX/UI/Stage Selected 1_Long_Ap_02.wav",
                "Assets/Action_RPG_SFX/UI/Stage Selected 1_Long_Ap_03.wav",
            });
            SetClips(serialized, "waveClearClips", new[]
            {
                "Assets/Action_RPG_SFX/Effects/Level Up 1_Rise_Effect_01 .wav",
                "Assets/Action_RPG_SFX/Effects/Level Up 1_Rise_Effect_02.wav",
                "Assets/Action_RPG_SFX/Effects/Level Up 1_Rise_Effect_03.wav",
            });
            SetClips(serialized, "attackClips", new[]
            {
                "Assets/Action_RPG_SFX/Attack/Weapon Swing_Dagger_Assassin_01.wav",
                "Assets/Action_RPG_SFX/Attack/Weapon Swing_Dagger_Assassin_02.wav",
                "Assets/Action_RPG_SFX/Attack/Weapon Swing_Dagger_Assassin_03.wav",
            });
            SetClips(serialized, "hitClips", new[]
            {
                "Assets/Action_RPG_SFX/Combat/Combat_Hit_Cut_01.wav",
                "Assets/Action_RPG_SFX/Combat/Combat_Hit_Cut_02.wav",
                "Assets/Action_RPG_SFX/Combat/Combat_Hit_Cut_03.wav",
            });
            SetClips(serialized, "dodgeClips", new[]
            {
                "Assets/Action_RPG_SFX/Movement/Dodge_Movement_Whoosh_01.wav",
                "Assets/Action_RPG_SFX/Movement/Dodge_Movement_Whoosh_02.wav",
                "Assets/Action_RPG_SFX/Movement/Dodge_Movement_Whoosh_03.wav",
            });
            SetClips(serialized, "trapClips", new[]
            {
                "Assets/Action_RPG_SFX/Combat/Bone Break_Crack_Combat_01.wav",
                "Assets/Action_RPG_SFX/Combat/Bone Break_Crack_Combat_02.wav",
                "Assets/Action_RPG_SFX/Combat/Bone Break_Crack_Combat_03.wav",
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
