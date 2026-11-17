using Code.Core;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 유물 줄(uGUI, A안, 왼쪽 위 장부 바로 아래). 가진 유물을 고정 칸(<see cref="ArtifactRowPresenter.SlotCount"/>)에 산 순서대로 놓고,
    /// 성립한 조합에 든 유물은 금색 테두리로 보인다. 칸에 마우스를 올리면 오른쪽에 설명 카드(이름·대상·설명·조합)를 띄운다.
    /// 판단은 <see cref="ArtifactPanelView"/>(보유 목록·열린 날)가 하고, 여기서는 그리기만 한다. 유물이 열리기 전(2일차 전)에는 줄 전체를 숨긴다.
    ///
    /// 예전에는 유물을 사도 보이는 곳이 없어 무엇을 가졌는지, 조합이 맞았는지 알 수 없었다.
    /// </summary>
    public sealed class ArtifactRowView : MonoBehaviour
    {
        [SerializeField] private ArtifactPanelView panel;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text comboSummary;
        [SerializeField] private TMP_Text emptyHint;
        [SerializeField] private ArtifactSlotView[] slots;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailTarget;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private TMP_Text detailCombos;

        private ArtifactSlotView _hovered;

        public bool IsVisible => root != null && root.activeSelf;
        public bool IsDetailOpen => detailRoot != null && detailRoot.activeSelf;

        /// <summary>그 칸의 설명 카드를 연다(마우스 올림과 같은 경로, 실측용).</summary>
        public void ShowDetail(int slotIndex)
        {
            if (slots != null && slotIndex >= 0 && slotIndex < slots.Length && slots[slotIndex] != null && slots[slotIndex].Artifact != null)
                HandleEntered(slots[slotIndex]);
        }

        private void Awake()
        {
            if (panel == null)
                panel = SceneReferenceFallback.Find<ArtifactPanelView>(this, nameof(panel));
            if (detailRoot != null)
                detailRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (panel != null)
                panel.Changed += Render;
            foreach (var slot in slots)
            {
                slot.PointerEntered += HandleEntered;
                slot.PointerExited += HandleExited;
            }

            Render();
        }

        private void OnDisable()
        {
            if (panel != null)
                panel.Changed -= Render;
            foreach (var slot in slots)
            {
                slot.PointerEntered -= HandleEntered;
                slot.PointerExited -= HandleExited;
            }

            HideDetail();
        }

        private void Render()
        {
            if (panel == null)
                return;

            var model = ArtifactRowPresenter.Build(panel.Artifacts, panel.IsUnlocked, panel.Combos);
            root.SetActive(model.Visible);
            if (!model.Visible)
            {
                HideDetail();
                return;
            }

            header.text = model.Header;
            comboSummary.text = model.ComboSummary;
            emptyHint.text = model.EmptyHint;
            for (var i = 0; i < slots.Length; i++)
                slots[i].Bind(i < model.Slots.Count ? model.Slots[i] : null);

            // 보던 유물이 목록에서 빠졌으면 카드를 닫고, 그대로면 조합 상태가 바뀌었을 수 있으니 다시 그린다.
            if (_hovered != null)
            {
                if (_hovered.Artifact == null)
                    HideDetail();
                else
                    HandleEntered(_hovered);
            }
        }

        private void HandleEntered(ArtifactSlotView slot)
        {
            _hovered = slot;
            var detail = ArtifactRowPresenter.BuildDetail(slot.Artifact, panel.Artifacts, panel.Combos);
            detailName.text = detail.Name;
            detailTarget.text = detail.Target;
            detailDescription.text = detail.Description;
            detailCombos.text = detail.Combos;
            detailRoot.SetActive(true);
        }

        private void HandleExited(ArtifactSlotView slot)
        {
            if (slot == _hovered)
                HideDetail();
        }

        private void HideDetail()
        {
            _hovered = null;
            if (detailRoot != null)
                detailRoot.SetActive(false);
        }
    }
}
