using Blade.Core;
using UnityEngine;

namespace Code.UI.Toolkit
{
    /// <summary>인게임의 운영 HUD만 UI Toolkit으로 교체한다.</summary>
    public static class GameplayHudToolkitBootstrap
    {
        private const string DocumentPath = "UI/Toolkit/GameplayHud";
        private const string RootName = "GameplayHudToolkitUi";
        private const int SortingOrder = 100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            ToolkitDocumentSpawner.Install<GameplayHudToolkitView>(
                TitleMenuActions.GameSceneName, DocumentPath, RootName, SortingOrder, HideReplacedViews);
        }

        /// <summary>
        /// 이번 전환 범위는 운영 자금 HUD와 배속 버튼뿐이다.
        /// 나머지 UGUI 패널과 전투 조작은 계속 살아 있어 다음 단계에서 하나씩 옮길 수 있다.
        /// </summary>
        private static void HideReplacedViews()
        {
            foreach (var view in Object.FindObjectsByType<GoldCostView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                view.gameObject.SetActive(false);
            foreach (var view in Object.FindObjectsByType<TimeSpeedView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                view.gameObject.SetActive(false);
        }
    }
}
