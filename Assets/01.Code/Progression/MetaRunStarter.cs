using Code.Manager;
using Code.Persistence;
using Code.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Progression
{
    /// <summary>
    /// 게임 씬이 열릴 때 시작 보정을 건다.
    ///
    /// 판 저장이 없으면 새 판이다 — 장착한 보정을 이번 판 보정으로 확정하고 전부 건다.
    /// 판 저장이 있으면 이어하기다 — 금화·민심처럼 한 번 받은 보정은 이미 저장에 들어 있으므로
    /// 판 내내 유지되는 보정(부활 마력)만 다시 건다. 씬 배선 없이 씬이 열리는 순간에 붙는다.
    /// </summary>
    public static class MetaRunStarter
    {
        private static bool _listening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!_listening)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _listening = true;
            }

            // 에디터에서 게임 씬을 바로 재생하면 첫 씬의 sceneLoaded는 이미 지나갔다.
            if (SceneManager.GetActiveScene().name == TitleMenuActions.GameSceneName)
                ApplyForCurrentScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TitleMenuActions.GameSceneName)
                ApplyForCurrentScene();
        }

        private static void ApplyForCurrentScene()
        {
            if (!RunSaveSystem.HasSave)
            {
                foreach (var value in MetaProfileStore.BeginNewRun())
                    Apply((MetaModifier)value, true);
                return;
            }

            var run = MetaProfileStore.Profile.currentRun;
            if (run == null || !run.active || run.modifiers == null)
                return;

            foreach (var value in run.modifiers)
            {
                var modifier = (MetaModifier)value;
                if (MetaProgressRules.IsPersistent(modifier))
                    Apply(modifier, false);
            }
        }

        private static void Apply(MetaModifier modifier, bool newRun)
        {
            switch (modifier)
            {
                case MetaModifier.EmergencyFund:
                    if (newRun)
                        CostManager.Current?.AddGold(MetaProgressRules.EmergencyFundGold);
                    break;
                case MetaModifier.SteadyMorale:
                    if (newRun)
                        MoralePolicyManager.Current?.ApplyBonusMorale(MetaProgressRules.SteadyMoraleBonus, "시작 보정");
                    break;
                case MetaModifier.ManaReserve:
                    UnitRevivalSystem.Current?.AddMaxManaBonus(MetaProgressRules.ManaReserveBonus);
                    break;
            }
        }
    }
}
