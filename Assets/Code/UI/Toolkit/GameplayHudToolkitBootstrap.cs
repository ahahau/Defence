using Blade.Core;
using Code.UI;
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
        /// HUD·배속·일시정지 메뉴는 Toolkit 문서가 소유한다.
        /// 같은 ESC 입력을 받는 옛 설정 창은 꺼 두고, 전투와 노드 UGUI만 단계적으로 남긴다.
        /// </summary>
        private static void HideReplacedViews()
        {
            foreach (var view in Object.FindObjectsByType<GoldCostView>(FindObjectsInactive.Exclude))
                view.gameObject.SetActive(false);
            foreach (var view in Object.FindObjectsByType<TimeSpeedView>(FindObjectsInactive.Exclude))
                view.gameObject.SetActive(false);
            foreach (var view in Object.FindObjectsByType<SettingsPanelView>(FindObjectsInactive.Exclude))
                view.enabled = false;
        }
    }
}
