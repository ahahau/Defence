using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Audio
{
    /// <summary>
    /// 버튼을 누를 때 소리가 나게 한다.
    ///
    /// 씬에 미리 붙여두지 않고 실행 시점에 스스로 올라온다 — 버튼이 56개인 데다
    /// 상점 칸이나 고용 목록처럼 실행 중에 만들어지는 것도 있어서, 씬에 한 번 배선해두는 방식으로는
    /// 나중에 생긴 버튼을 놓친다. 대신 짧은 주기로 새로 생긴 버튼만 찾아 붙인다.
    ///
    /// Unity에는 '아무 버튼이나 눌렸을 때'를 알려주는 전역 신호가 없어서 이 방법을 쓴다.
    /// </summary>
    public sealed class UiSfxInstaller : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;

        private readonly HashSet<Button> bound = new();
        private readonly Dictionary<Transform, bool> panelVisible = new();
        private readonly List<Transform> panels = new();
        private float nextScanTime;
        private bool panelsCollected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("UI SFX Installer");
            host.AddComponent<UiSfxInstaller>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
                return;

            nextScanTime = Time.unscaledTime + ScanInterval;
            Scan();
            ScanPanels();
        }

        /// <summary>
        /// 패널이 열리고 닫히는 소리.
        ///
        /// Unity에는 SetActive를 가로챌 방법이 없어서, 이름에 Panel이 붙은 것들을 미리 모아두고
        /// 보이는 상태가 바뀌는지 지켜본다. 규칙에 기대는 방식이라 이름이 다른 창은 놓치는데,
        /// 놓쳐도 조용할 뿐 잘못된 소리가 나지는 않는다.
        /// </summary>
        private void ScanPanels()
        {
            if (!panelsCollected)
            {
                panelsCollected = true;
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                foreach (var child in canvas.GetComponentsInChildren<Transform>(true))
                    if (child != canvas.transform && child.name.Contains("Panel"))
                    {
                        panels.Add(child);
                        panelVisible[child] = child.gameObject.activeInHierarchy;
                    }
            }

            foreach (var panel in panels)
            {
                if (panel == null)
                    continue;

                var visible = panel.gameObject.activeInHierarchy;
                if (!panelVisible.TryGetValue(panel, out var was) || was == visible)
                {
                    panelVisible[panel] = visible;
                    continue;
                }

                panelVisible[panel] = visible;
                GameSfxPlayer.Play(visible ? GameSfxCue.UiOpen : GameSfxCue.UiClose);
            }
        }

        private void Scan()
        {
            var selectables = Selectable.allSelectablesArray;
            foreach (var selectable in selectables)
            {
                if (selectable is not Button button)
                    continue;

                if (!bound.Add(button))
                    continue;

                button.onClick.AddListener(() => GameSfxPlayer.Play(GameSfxCue.UiClick));
            }

            // 파괴된 버튼이 계속 쌓이지 않게 가끔 비운다.
            // 다시 스캔하면 살아 있는 것만 도로 채워진다.
            if (bound.Count > 512)
                bound.Clear();
        }
    }
}
