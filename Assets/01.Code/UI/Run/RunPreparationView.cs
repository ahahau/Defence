using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>배치 전용 HUD. 전달받은 문구를 표시하고 조작을 중계한다.</summary>
    public class RunPreparationView : MonoBehaviour
    {
        [SerializeField] private GameObject[] preparationParts = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] battleParts = Array.Empty<GameObject>();
        [SerializeField] private TMP_Text[] partyCards = Array.Empty<TMP_Text>();
        [SerializeField, Tooltip("partyCards와 같은 순서의 카드 테두리. 빈 칸을 숨길 때 쓴다.")] private GameObject[] partyFrames = Array.Empty<GameObject>();
        [SerializeField] private RunPreparationCard[] minionCards = Array.Empty<RunPreparationCard>();
        [SerializeField] private TMP_Text mana;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private TMP_Text tip;
        [SerializeField] private UiFrameButton startButton;
        [SerializeField] private UiFrameButton restoreButton;
        [SerializeField] private RectTransform dragGhost;
        [SerializeField] private TMP_Text dragLabel;
        [SerializeField] private RectTransform canvas;

        private UnityEngine.Events.UnityAction _start;
        private UnityEngine.Events.UnityAction _restore;
        private Action[] _clicks;
        private Action[] _begins;
        private Action<Vector2>[] _drops;
        private Action<Vector2>[] _drags;
        public event Action StartClicked;
        public event Action RestoreClicked;
        public event Action<int> Selected;
        public event Action<int, Vector2> Dropped;
        public int PartyCapacity => partyCards.Length;
        public int MinionCapacity => minionCards.Length;

        /// <summary>첫 판 안내가 가리킬 부하 준비 카드. 없는 번호면 null.</summary>
        public RectTransform MinionCardRect(int index) =>
            index >= 0 && index < minionCards.Length && minionCards[index] != null ? (RectTransform)minionCards[index].transform : null;

        private void OnEnable()
        {
            EnsureRuntimeState();
            startButton?.Button.onClick.AddListener(_start);
            restoreButton?.Button.onClick.AddListener(_restore);
            for (int i = 0; i < minionCards.Length; i++)
            {
                minionCards[i].Clicked += _clicks[i];
                minionCards[i].DragStarted += _begins[i];
                minionCards[i].Dropped += _drops[i];
                minionCards[i].Dragged += _drags[i];
            }
        }

        private void OnDisable()
        {
            if (_start == null) return;
            startButton?.Button.onClick.RemoveListener(_start);
            restoreButton?.Button.onClick.RemoveListener(_restore);
            if (_clicks == null) return;
            for (int i = 0; i < minionCards.Length; i++)
            {
                minionCards[i].Clicked -= _clicks[i];
                minionCards[i].DragStarted -= _begins[i];
                minionCards[i].Dropped -= _drops[i];
                minionCards[i].Dragged -= _drags[i];
            }
        }

        private void EnsureRuntimeState()
        {
            _start ??= () => StartClicked?.Invoke();
            _restore ??= () => RestoreClicked?.Invoke();
            if (_clicks != null && _clicks.Length == minionCards.Length) return;
            _clicks = new Action[minionCards.Length];
            _begins = new Action[minionCards.Length];
            _drops = new Action<Vector2>[minionCards.Length];
            _drags = new Action<Vector2>[minionCards.Length];
            for (int i = 0; i < minionCards.Length; i++)
            {
                int index = i;
                _clicks[i] = () => Selected?.Invoke(index);
                _begins[i] = () => { Selected?.Invoke(index); if (dragGhost != null) dragGhost.gameObject.SetActive(true); };
                _drags[i] = MoveGhost;
                _drops[i] = point => { HideGhost(); Dropped?.Invoke(index, point); };
            }
        }

        public void Show(bool preparing)
        {
            for (int i = 0; i < preparationParts.Length; i++) preparationParts[i].SetActive(preparing);
            for (int i = 0; i < battleParts.Length; i++) battleParts[i].SetActive(!preparing);
            HideGhost();
        }

        public void SetParty(int index, string text)
        {
            if (index >= partyCards.Length) return;
            partyCards[index].SetText(text);
            // 빈 칸은 테두리까지 숨긴다. 카드는 위에서부터 채우므로 남은 카드 자리는 움직이지 않는다.
            if (index < partyFrames.Length && partyFrames[index] != null) partyFrames[index].SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>준비 화면 아래 띠. 비우면 씬에 적힌 글을 그대로 둔다.</summary>
        public void SetTip(string text)
        {
            if (tip != null && !string.IsNullOrEmpty(text)) tip.SetText(text);
        }

        public void SetMinion(int index, string text, Color color, bool selected, bool available = true)
        {
            if (index < minionCards.Length)
            {
                minionCards[index].gameObject.SetActive(available);
                if (available) minionCards[index].Show(text, color, selected);
            }
            if (selected && dragLabel != null) dragLabel.SetText(text);
        }

        public void SetSummary(string text, string information, bool canRestore)
        {
            mana?.SetText(text);
            detail?.SetText(information);
            if (restoreButton != null) restoreButton.Button.interactable = canRestore;
        }

        public void MoveGhost(Vector2 screen)
        {
            if (dragGhost == null || canvas == null) return;
            dragGhost.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            dragGhost.anchoredPosition = local + new Vector2(20f, -20f);
        }

        public void HideGhost()
        {
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }
    }
}
