using Code.Dialogue;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 사건·튜토리얼 대화 상자(uGUI, A안). 진행 판단은 <see cref="DialogueRunner"/>·<see cref="DialogueView"/>가 하고,
    /// 이 화면은 <see cref="DialogueView.Current"/>를 그리고 버튼 입력만 돌려준다.
    ///
    /// 본문은 초당 <see cref="revealCharsPerSecond"/>글자씩 흘러나온다(실제 시간). 흐르는 중에 "다음"을 누르면
    /// 먼저 끝까지 보여 준다. 선택지가 있으면 "다음"을 숨긴다. 사건처럼 뒤를 막는 대화는 덮개가 클릭을 삼키고,
    /// 튜토리얼 강조 중에는 덮개 대신 네 조각 어둠이 강조할 자리만 남긴다.
    /// </summary>
    public sealed class DialogueUiView : MonoBehaviour
    {
        [SerializeField] private DialogueView dialogueView;
        [SerializeField] private GameObject root;
        [SerializeField, Tooltip("뒤 화면을 덮고 클릭을 막는 덮개")] private GameObject scrim;
        [SerializeField] private UiSpotlightDim spotlight;
        [SerializeField] private TMP_Text speaker;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private TMP_Text body;
        [SerializeField] private DialogueChoiceButton[] choices;
        [SerializeField] private UiFrameButton nextButton;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField, Tooltip("닫기를 자리 지킨 채 숨기는 투명도")] private CanvasGroup closeGroup;

        [SerializeField, Min(1f), Tooltip("초당 글자 수. 한국어는 한 글자가 커서 영문 기준보다 느리게 잡는다.")]
        private float revealCharsPerSecond = 45f;
        [SerializeField, Tooltip("선택지가 하나씩 들어오는 간격(초)")]
        private float choiceStaggerSeconds = 0.045f;

        private int _shownVersion = -1;
        private int _totalCharacters;
        private float _revealProgress;
        private UnityEngine.Events.UnityAction[] _choiceHandlers;

        /// <summary>본문이 아직 흘러나오는 중인가.</summary>
        public bool IsRevealing => body != null && body.maxVisibleCharacters < _totalCharacters;

        private void Awake()
        {
            _choiceHandlers = new UnityEngine.Events.UnityAction[choices != null ? choices.Length : 0];
            for (var i = 0; i < _choiceHandlers.Length; i++)
            {
                var index = i;
                _choiceHandlers[i] = () => HandleChoice(index);
            }
        }

        private void OnEnable()
        {
            if (dialogueView == null)
            {
                Debug.LogWarning($"{nameof(DialogueUiView)}: dialogueView가 비어 있어 대화를 그리지 않습니다.", this);
                return;
            }

            nextButton?.Button.onClick.AddListener(HandleNext);
            closeButton?.Button.onClick.AddListener(HandleClose);
            for (var i = 0; i < _choiceHandlers.Length; i++)
                if (choices[i] != null)
                    choices[i].Frame.Button.onClick.AddListener(_choiceHandlers[i]);

            dialogueView.Changed += Render;
            Render();
        }

        private void OnDisable()
        {
            if (dialogueView == null)
                return;

            nextButton?.Button.onClick.RemoveListener(HandleNext);
            closeButton?.Button.onClick.RemoveListener(HandleClose);
            for (var i = 0; i < _choiceHandlers.Length; i++)
                if (choices[i] != null)
                    choices[i].Frame.Button.onClick.RemoveListener(_choiceHandlers[i]);

            dialogueView.Changed -= Render;
        }

        private void Update()
        {
            if (!IsRevealing)
                return;

            // 대화는 배속·일시정지와 무관하게 같은 속도로 읽혀야 한다.
            _revealProgress += revealCharsPerSecond * Time.unscaledDeltaTime;
            var visible = Mathf.Min(_totalCharacters, Mathf.FloorToInt(_revealProgress));
            if (visible != body.maxVisibleCharacters)
                body.maxVisibleCharacters = visible;
        }

        private void Render()
        {
            var visible = dialogueView.IsVisible;
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);

            if (!visible)
            {
                _shownVersion = -1;
                CompleteReveal();
                ClearChoices();
                spotlight?.Hide();
                return;
            }

            var state = dialogueView.Current;
            if (_shownVersion != dialogueView.Version)
            {
                _shownVersion = dialogueView.Version;
                SetText(speaker, state.SpeakerName);
                SetText(title, state.Title);
                SetText(progress, state.Progress);
                StartReveal(state.Body);
                BuildChoices();
            }

            var hasChoices = dialogueView.Choices.Count > 0;
            nextButton?.SetVisible(state.NextVisible && !hasChoices);
            if (closeButton != null)
            {
                // 닫기는 자리를 지킨 채 숨긴다(머리 줄이 흔들리지 않게).
                closeButton.Button.interactable = state.CloseVisible;
                if (closeGroup != null)
                    closeGroup.alpha = state.CloseVisible ? 1f : 0f;
            }

            if (state.SpotlightVisible)
                spotlight?.Show(state.SpotlightRect, state.SpotlightPadding);
            else
                spotlight?.Hide();

            // 강조하는 동안은 덮개를 끈다. 강조한 자리를 눌러야 다음으로 가기 때문이다.
            if (scrim != null)
                scrim.SetActive(state.BlocksBackground && !state.SpotlightVisible);
        }

        private void StartReveal(string text)
        {
            if (body == null)
                return;

            body.text = text ?? string.Empty;
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();
            // 서식 태그는 글자로 세지 않는다(TMP가 걸러 센다).
            _totalCharacters = body.textInfo.characterCount;
            _revealProgress = 0f;
        }

        private void CompleteReveal()
        {
            if (body == null)
                return;

            _totalCharacters = 0;
            body.maxVisibleCharacters = 99999;
        }

        private void BuildChoices()
        {
            var list = dialogueView.Choices;
            for (var i = 0; i < choices.Length; i++)
            {
                if (choices[i] == null)
                    continue;

                if (i < list.Count)
                    choices[i].Bind(list[i], 0.016f + i * choiceStaggerSeconds);
                else
                    choices[i].Clear();
            }

            if (list.Count > choices.Length)
                Debug.LogWarning($"{nameof(DialogueUiView)}: 선택지 {list.Count}개 중 {choices.Length}개만 그립니다.", this);
        }

        private void ClearChoices()
        {
            foreach (var choice in choices)
                choice?.Clear();
        }

        private void HandleNext()
        {
            if (IsRevealing)
            {
                CompleteReveal();
                return;
            }

            dialogueView.Next();
        }

        private void HandleClose() => dialogueView.Close();

        private void HandleChoice(int index) => dialogueView.Choose(index);

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != (value ?? string.Empty))
                label.text = value ?? string.Empty;
        }
    }
}
