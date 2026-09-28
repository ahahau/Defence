using Code.UI;
using UnityEngine;

namespace Code.UI.Toolkit
{
    public static class PolicyChoiceToolkitBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            ToolkitDocumentSpawner.Install<PolicyChoiceToolkitView>(TitleMenuActions.GameSceneName,
                "UI/Toolkit/PolicyChoicePanel", "PolicyChoiceToolkitUi", 240, HideLegacyView);
        }

        private static void HideLegacyView()
        {
            foreach (var view in Object.FindObjectsByType<PolicyChoicePanelView>(FindObjectsInactive.Exclude))
                view.enabled = false;
        }
    }
}
