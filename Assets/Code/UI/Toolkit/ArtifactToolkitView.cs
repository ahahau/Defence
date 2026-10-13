using System.Collections.Generic;
using Code.Artifacts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 가진 유물 줄(UI Toolkit). 아이콘을 늘어놓고, 마우스를 올리면 이름과 설명을 툴팁으로 보여 준다.
    /// 인벤토리 구독과 해금 시점 판정은 씬의 <see cref="ArtifactPanelView"/>가 맡고 여기로 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ArtifactToolkitView : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/ArtifactBarScreen";
        private const string RootName = "ArtifactToolkitUi";

        private const string RootElement = "artifact-root";
        private const string BarElement = "artifact-bar";
        private const string IconsElement = "artifact-icons";
        private const string TooltipElement = "artifact-tooltip";
        private const string TooltipNameLabel = "artifact-tooltip-name";
        private const string TooltipDescriptionLabel = "artifact-tooltip-description";
        private const string IconClass = "artifact-icon";
        private const float TooltipGap = 8f;

        private static ArtifactToolkitView _instance;

        private VisualElement _root;
        private VisualElement _bar;
        private VisualElement _icons;
        private VisualElement _tooltip;
        private Label _tooltipName;
        private Label _tooltipDescription;
        private readonly List<VisualElement> _iconPool = new();
        private readonly List<ArtifactDataSO> _shown = new();
        private bool _unlocked;

        /// <summary>유물 줄 문서를 지금 씬에 세운다(이미 있으면 그것).</summary>
        public static ArtifactToolkitView Ensure()
        {
            if (_instance == null)
                _instance = ToolkitScreenSO.Spawn<ArtifactToolkitView>(ScreenResourcePath, RootName);
            return _instance;
        }

        /// <summary>가진 유물을 다시 그린다.</summary>
        public void SetArtifacts(IEnumerable<ArtifactDataSO> artifacts)
        {
            _shown.Clear();
            if (artifacts != null)
            {
                foreach (var artifact in artifacts)
                {
                    if (artifact != null)
                        _shown.Add(artifact);
                }
            }

            for (var i = 0; i < _shown.Count; i++)
                Bind(GetIcon(i), _shown[i]);
            for (var i = _shown.Count; i < _iconPool.Count; i++)
                _iconPool[i].style.display = DisplayStyle.None;

            HideTooltip();
            ApplyVisibility();
        }

        /// <summary>유물 기능이 열리기 전(해금일 전)에는 줄을 통째로 숨긴다.</summary>
        public void SetUnlocked(bool unlocked)
        {
            _unlocked = unlocked;
            if (!unlocked)
                HideTooltip();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            _root = tree.Q<VisualElement>(RootElement) ?? tree;
            _bar = tree.Q<VisualElement>(BarElement);
            _icons = tree.Q<VisualElement>(IconsElement);
            _tooltip = tree.Q<VisualElement>(TooltipElement);
            _tooltipName = tree.Q<Label>(TooltipNameLabel);
            _tooltipDescription = tree.Q<Label>(TooltipDescriptionLabel);
            ToolkitPointerBlockers.Register(_bar);
            ApplyVisibility();
        }

        private void OnDisable() => ToolkitPointerBlockers.Unregister(_bar);

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private VisualElement GetIcon(int index)
        {
            while (_iconPool.Count <= index)
            {
                var icon = new VisualElement();
                icon.AddToClassList(IconClass);
                var slot = _iconPool.Count;
                icon.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(slot));
                icon.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
                _icons?.Add(icon);
                _iconPool.Add(icon);
            }

            return _iconPool[index];
        }

        private static void Bind(VisualElement icon, ArtifactDataSO artifact)
        {
            icon.style.display = DisplayStyle.Flex;
            if (artifact.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(artifact.Icon);
                icon.style.backgroundColor = StyleKeyword.Null;
            }
            else
            {
                // 아이콘이 아직 없는 유물은 데이터의 색으로 자리만 표시한다.
                icon.style.backgroundImage = StyleKeyword.None;
                icon.style.backgroundColor = artifact.IconColor;
            }
        }

        private void ShowTooltip(int slot)
        {
            if (_tooltip == null || slot < 0 || slot >= _shown.Count)
                return;

            var artifact = _shown[slot];
            if (_tooltipName != null) _tooltipName.text = artifact.DisplayName;
            if (_tooltipDescription != null) _tooltipDescription.text = artifact.Description;

            // 아이콘 바로 아래에 띄운다. 패널 기준 좌표라 해상도와 상관없이 같은 자리다.
            var anchor = _iconPool[slot].worldBound;
            var origin = _root.WorldToLocal(new Vector2(anchor.xMin, anchor.yMax + TooltipGap));
            _tooltip.style.left = origin.x;
            _tooltip.style.top = origin.y;
            _tooltip.style.visibility = Visibility.Visible;
        }

        private void HideTooltip()
        {
            if (_tooltip != null)
                _tooltip.style.visibility = Visibility.Hidden;
        }

        private void ApplyVisibility()
        {
            if (_root == null)
                return;

            _root.style.display = _unlocked && _shown.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
