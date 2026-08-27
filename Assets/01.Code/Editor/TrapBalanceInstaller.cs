#if UNITY_EDITOR
using _01.Code.Buildings;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 함정 수치를 역할별로 다시 잡는다.
    ///
    /// 종전에는 발동 확률과 부상 확률을 따로 굴려서, 압쇄 트랩의 기절은 0.20 × 0.30 = 6%로
    /// 사실상 일어나지 않았다. 함정마다 상태이상이 배정돼 있는데도 그 역할이 눈에 보이지 않았다.
    /// 이제 걸리면 그 함정의 특기는 반드시 발동한다 — 확률은 '걸리느냐' 하나로만 판단한다.
    ///
    /// 축은 둘이다: 자주 걸리지만 약한 함정 vs 가끔 걸리지만 크게 무는 함정.
    /// </summary>
    public static class TrapBalanceInstaller
    {
        private readonly struct Spec
        {
            public Spec(string displayName, int damage, float triggerChance, float damagePerDay, string role)
            {
                DisplayName = displayName;
                Damage = damage;
                TriggerChance = triggerChance;
                DamagePerDay = damagePerDay;
                Role = role;
            }

            public string DisplayName { get; }
            public int Damage { get; }
            public float TriggerChance { get; }
            public float DamagePerDay { get; }
            public string Role { get; }
        }

        private static readonly Spec[] Specs =
        {
            // 싼 발판 — 거의 항상 걸리지만 한 대는 가볍다. 길목을 오래 갉는 역할.
            new("올가미 트랩", 4, 0.85f, 0.35f, "약하게 자주"),
            new("마름쇠 트랩", 6, 0.75f, 0.45f, "약하게 자주"),
            new("가시 트랩", 8, 0.70f, 0.50f, "기본"),

            // 중간 — 발동은 반반이지만 특기가 확실하다.
            new("매복 그물", 5, 0.70f, 0.35f, "속박 — 발을 묶어 유닛에게 시간을 번다"),
            new("칼날 트랩", 12, 0.55f, 0.60f, "출혈"),
            new("갑옷분쇄 트랩", 11, 0.55f, 0.55f, "방어 파괴 — 뒤이은 유닛 공격이 더 아프다"),

            // 비싼 한 방 — 가끔 걸리지만 물면 크다.
            new("압쇄 트랩", 20, 0.45f, 0.80f, "기절"),
            new("처형 톱날", 24, 0.45f, 0.95f, "노출")
        };

        public static void Install()
        {
            var applied = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:BuildingDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.Prefab == null)
                    continue;

                var trap = data.Prefab.GetComponent<Trap>();
                if (trap == null)
                    continue;

                if (!TryFindSpec(data.DisplayName, out var spec))
                    continue;

                var serialized = new SerializedObject(trap);
                serialized.FindProperty("damage").intValue = spec.Damage;
                serialized.FindProperty("bonusDamage").intValue = 0;
                serialized.FindProperty("triggerChance").floatValue = spec.TriggerChance;
                serialized.FindProperty("damagePerDay").floatValue = spec.DamagePerDay;
                // 걸렸으면 그 함정의 특기는 반드시 나온다. 두 번 굴리면 역할이 보이지 않는다.
                serialized.FindProperty("injuryChance").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(trap);
                EditorUtility.SetDirty(data.Prefab);
                applied++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Rebalanced {applied} traps.");
        }

        private static bool TryFindSpec(string displayName, out Spec spec)
        {
            foreach (var candidate in Specs)
            {
                if (candidate.DisplayName == displayName)
                {
                    spec = candidate;
                    return true;
                }
            }

            spec = default;
            return false;
        }
    }
}
#endif
