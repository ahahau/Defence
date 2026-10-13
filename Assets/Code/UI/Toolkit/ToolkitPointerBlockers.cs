using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 지도 위에 뜨는 UI Toolkit 패널을 클릭이 뚫고 지나가지 않게 한다.
    ///
    /// 지도 클릭은 uGUI 레이캐스트로만 UI를 걸러서, Toolkit 패널의 버튼을 누르면 그 아래 방까지 눌린다.
    /// 지도 위에 뜨는 패널은 카드 요소를 여기 등록하고, 지도 입력은 포인터가 그 위에 있으면 무시한다.
    /// 화면 전체를 덮는 HUD 루트는 등록하지 않는다 — 등록하면 지도가 통째로 막힌다.
    /// </summary>
    public static class ToolkitPointerBlockers
    {
        private static readonly List<VisualElement> Blockers = new();

        public static void Register(VisualElement element)
        {
            if (element != null && !Blockers.Contains(element))
                Blockers.Add(element);
        }

        public static void Unregister(VisualElement element) => Blockers.Remove(element);

        /// <summary>화면 좌표(왼쪽 아래 원점)가 보이는 블로커 위에 있는가.</summary>
        public static bool IsPointerOver(Vector2 screenPosition)
        {
            for (var i = Blockers.Count - 1; i >= 0; i--)
            {
                var element = Blockers[i];
                if (element == null || element.panel == null)
                {
                    Blockers.RemoveAt(i);
                    continue;
                }

                if (element.resolvedStyle.display == DisplayStyle.None || !IsShownInHierarchy(element))
                    continue;

                var panelPosition = RuntimePanelUtils.ScreenToPanel(
                    element.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
                if (element.worldBound.Contains(panelPosition))
                    return true;
            }

            return false;
        }

        private static bool IsShownInHierarchy(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }

            return true;
        }
    }
}
