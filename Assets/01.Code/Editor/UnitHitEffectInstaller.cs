using System.Collections.Generic;
using _01.Code.Combat;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 몬스터마다 다른 타격 이펙트를 붙인다.
    ///
    /// 나중에 들어온 넷(포자 버섯·동굴 거미·광포한 늑대·그림자 암살귀)은 창 코볼트가 쓰던
    /// 주먹 이펙트를 그대로 물려받아서, 포자를 뿌리든 물어뜯든 화면에는 똑같은 게 터진다.
    /// 무엇이 때렸는지 눈으로 구분이 안 되면 전투를 읽을 수 없다.
    ///
    /// 중첩 프리팹은 텍스트로 못 고친다. 자식 오브젝트의 fileID가 원본 프리팹 내부 번호에서
    /// 나오기 때문에 손으로 써넣으면 끊긴 참조가 된다. 그래서 에디터에서 PrefabUtility로 갈아끼운다.
    /// </summary>
    public static class UnitHitEffectInstaller
    {
        private const string FxChildName = "HitFX";
        private const string EtfxFolder = "Assets/Epic Toon FX/Prefabs/Combat/";

        /// <summary>프리팹 이름 → 갈아끼울 이펙트. 여기 없는 몬스터는 건드리지 않는다.</summary>
        private static readonly Dictionary<string, string> Assignments = new()
        {
            // 포자 버섯 — 포자를 뿌린다. 초록 가스가 퍼지는 게 제일 가깝다.
            ["Unit_Shroom"] = "Explosions/GasExplosion/GasExplosionGreen.prefab",
            // 동굴 거미 — 독니. 작고 날카롭게 한 번.
            ["Unit_Spider"] = "Sword/Hit/SwordHitMini/SwordHitMiniGreen.prefab",
            // 광포한 늑대 — 발톱. 베인 자국이 남아야 한다.
            ["Unit_Wolf"] = "Sword/Slash/SwordSlashThin/SwordSlashThinRed.prefab",
            // 그림자 암살귀 — 형체 없는 것이 스친다. 주먹 자국은 어울리지 않는다.
            ["Unit_Shade"] = "Explosions/EnergyExplosion/EnergyExplosionPink.prefab",
        };

        [MenuItem("Defence/FX/Assign Unit Hit Effects")]
        public static void AssignHitEffects()
        {
            var changed = 0;
            foreach (var (prefabName, fxRelativePath) in Assignments)
            {
                var path = FindUnitPrefab(prefabName);
                if (path == null)
                {
                    Debug.LogWarning($"[HitFX] {prefabName} 프리팹을 찾지 못했다.");
                    continue;
                }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(EtfxFolder + fxRelativePath);
                if (source == null)
                {
                    Debug.LogWarning($"[HitFX] {fxRelativePath} 이펙트가 없다.");
                    continue;
                }

                if (Swap(path, source))
                    changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HitFX] 타격 이펙트 {changed}종 교체 완료.");
        }

        private static string FindUnitPrefab(string prefabName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab",
                         new[] { "Assets/04.Prefab/Characters/Units" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == prefabName)
                    return path;
            }

            return null;
        }

        private static bool Swap(string prefabPath, GameObject fxSource)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var combatant = root.GetComponentInChildren<Combatant>(true);
                if (combatant == null)
                {
                    Debug.LogWarning($"[HitFX] {prefabPath}: Combatant이 없다.");
                    return false;
                }

                // 기존 HitFX의 자리와 크기를 그대로 물려받는다. 새로 재는 것보다 확실하다.
                var old = FindChild(root.transform, FxChildName);
                var parent = old != null ? old.parent : combatant.transform;
                var localPosition = old != null ? old.localPosition : Vector3.zero;
                var localRotation = old != null ? old.localRotation : Quaternion.Euler(0f, -90f, 90f);
                var localScale = old != null ? old.localScale : Vector3.one * 1.5f;

                if (old != null)
                    Object.DestroyImmediate(old.gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fxSource, parent);
                instance.name = FxChildName;
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;
                instance.transform.localScale = localScale;

                Silence(instance);

                var particles = instance.GetComponent<ParticleSystem>()
                                ?? instance.GetComponentInChildren<ParticleSystem>(true);
                if (particles == null)
                {
                    Debug.LogWarning($"[HitFX] {fxSource.name}에 ParticleSystem이 없다.");
                    return false;
                }

                var serialized = new SerializedObject(combatant);
                serialized.FindProperty("attackHitParticles").objectReferenceValue = particles;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[HitFX] {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} → {fxSource.name}");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// ETFX 프리팹은 켜지자마자 알아서 터지고 소리까지 낸다.
        /// 씬에 올려두는 물건이라 그대로 두면 전투가 시작되기도 전에 한 번씩 다 터진다.
        /// </summary>
        private static void Silence(GameObject instance)
        {
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.playOnAwake = false;
            }

            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true))
                audio.playOnAwake = false;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name == name)
                    return child;

            return null;
        }
    }
}
