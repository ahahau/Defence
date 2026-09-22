using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Dialogue
{
    public class DialogueView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform choiceRoot;
        [SerializeField] private Button choiceButtonPrefab;
        [SerializeField] private TutorialSpotlightView spotlightPrefab;
        [SerializeField] private TutorialSpotlightView sceneSpotlightView;
        [SerializeField, Min(0f)] private float choiceSlideDuration = 0.22f;
        [SerializeField] private float choiceSlideOffset = 280f;
        [SerializeField, Min(0f)] private float choiceStaggerDelay = 0.045f;
        [SerializeField, Min(0f)] private float choiceTopPadding = 14f;
        [SerializeField, Min(0f)] private float choiceSidePadding = 14f;
        [SerializeField, Min(1f)] private float choiceButtonHeight = 54f;
        [SerializeField, Min(0f)] private float choiceButtonSpacing = 10f;
        [SerializeField] private string choiceLeadText = string.Empty;
        [SerializeField] private Color spotlightDimColor = new(0f, 0f, 0f, 0.62f);
        
        private DialogueRunner runner;
        private Button boundNextButton;
        private Button boundCloseButton;
        private Graphic rootGraphic;
        private Color rootGraphicDefaultColor;
        private bool rootGraphicDefaultRaycastTarget;
        private bool hasRootGraphicDefaultColor;
        private RectTransform rootRect;
        private RectTransform spotlightRoot;
        private readonly RectTransform[] spotlightRects = new RectTransform[4];
        private readonly List<Button> choiceButtons = new();
        private readonly List<Coroutine> choiceAnimations = new();

        private void OnEnable()
        {
            BindButtonListeners();
        }

        private void OnDisable()
        {
            UnbindButtonListeners();
        }

        public void Initialize(DialogueRunner dialogueRunner)
        {
            runner = dialogueRunner;

            if (root == null)
                root = gameObject;

            rootGraphic = root.GetComponent<Graphic>();
            CaptureRootGraphicColor();
            rootRect = root.transform as RectTransform;

            if (isActiveAndEnabled)
                BindButtonListeners();
        }

        public void Bind(
            GameObject rootObject,
            TMP_Text title,
            TMP_Text speaker,
            TMP_Text body,
            TMP_Text progress,
            Button next,
            Button close,
            RectTransform choices = null,
            Button choicePrefab = null)
        {
            UnbindButtonListeners();
            ClearChoices();

            root = rootObject != null ? rootObject : gameObject;
            rootGraphic = root.GetComponent<Graphic>();
            CaptureRootGraphicColor();
            rootRect = root.transform as RectTransform;
            titleText = title;
            speakerText = speaker;
            bodyText = body;
            progressText = progress;
            nextButton = next;
            closeButton = close;
            choiceRoot = choices;
            choiceButtonPrefab = choicePrefab;

            if (isActiveAndEnabled)
                BindButtonListeners();
        }

        public void Show(DialogueDisplayData data)
        {
            if (root == null)
                root = gameObject;

            rootGraphic ??= root.GetComponent<Graphic>();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            BindButtonListeners();

            if (titleText != null)
                titleText.text = data.Title;

            if (speakerText != null)
                speakerText.text = data.SpeakerName;

            if (bodyText != null)
                RevealBody(BuildBodyText(data));

            if (progressText != null)
                progressText.text = data.Progress;

            BuildChoices(data.Choices);

            if (nextButton != null)
            {
                var hasChoices = data.HasChoices;
                nextButton.gameObject.SetActive(!hasChoices);
                nextButton.interactable = !hasChoices;
                nextButton.transform.SetAsLastSibling();
            }

        }

        public void SetBackgroundRaycastBlocking(bool blocksRaycasts)
        {
            if (root == null)
                root = gameObject;

            rootGraphic ??= root.GetComponent<Graphic>();
            CaptureRootGraphicColor();
            if (rootGraphic != null)
                rootGraphic.raycastTarget = blocksRaycasts;
        }

        public void SetNextButtonVisible(bool visible)
        {
            if (nextButton != null)
                nextButton.gameObject.SetActive(visible);
        }

        public void SetCloseButtonVisible(bool visible)
        {
            if (closeButton != null)
                closeButton.gameObject.SetActive(visible);
        }

        public void Hide()
        {
            if (this == null)
                return;

            if (root == null)
                root = gameObject;

            if (root == null)
                return;

            // 흘러나오던 것을 멈추지 않으면 revealRoutine 이 남아, 다음에 열었을 때
            // 첫 클릭이 "넘기기"가 아니라 "마저 보여 주기"로 먹힌다.
            StopReveal();
            ClearChoices();
            root.SetActive(false);
            HideSpotlight();
        }

        private void HandleNextClicked()
        {
            // 아직 흘러나오는 중이면 먼저 끝까지 보여 준다. 읽던 문장이 사라지고 다음 줄로
            // 넘어가면 놓친 사람이 되돌릴 방법이 없다. 한 번 더 눌러야 넘어간다.
            if (IsRevealing)
            {
                CompleteReveal();
                return;
            }

            runner?.Next();
        }

        // ── 본문 타자 효과 ──────────────────────────────────────────────

        /// <summary>초당 글자 수. 한국어는 한 글자가 크므로 영문 기준보다 느리게 잡는다.</summary>
        [SerializeField, Min(1f)] private float revealCharsPerSecond = 45f;

        private Coroutine revealRoutine;

        private bool IsRevealing => revealRoutine != null;

        /// <summary>
        /// 본문을 한 글자씩 드러낸다. 글자를 잘라 넣지 않고 <c>maxVisibleCharacters</c>만 움직인다 —
        /// 문자열을 매 프레임 다시 만들면 줄바꿈이 계속 다시 잡혀 글이 출렁인다.
        /// </summary>
        private void RevealBody(string content)
        {
            if (bodyText == null)
                return;

            StopReveal();
            bodyText.text = content;
            bodyText.maxVisibleCharacters = 0;

            if (!isActiveAndEnabled || string.IsNullOrEmpty(content))
            {
                bodyText.maxVisibleCharacters = int.MaxValue;
                return;
            }

            revealRoutine = StartCoroutine(RevealRoutine());
        }

        private System.Collections.IEnumerator RevealRoutine()
        {
            // 글자 수는 태그를 뺀 값이라 TMP가 계산해 줄 때까지 기다린다.
            bodyText.ForceMeshUpdate();
            var total = bodyText.textInfo.characterCount;
            var shown = 0f;

            while (shown < total)
            {
                // 대화는 배속·일시정지와 무관하게 같은 속도로 읽혀야 한다.
                shown += revealCharsPerSecond * Time.unscaledDeltaTime;
                bodyText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
                yield return null;
            }

            bodyText.maxVisibleCharacters = int.MaxValue;
            revealRoutine = null;
        }

        private void CompleteReveal()
        {
            StopReveal();
            if (bodyText != null)
                bodyText.maxVisibleCharacters = int.MaxValue;
        }

        private void StopReveal()
        {
            if (revealRoutine == null)
                return;

            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        public void SetSpotlightScreenRect(Rect screenRect, float padding = 32f)
        {
            if (root == null)
                root = gameObject;

            rootRect ??= root.transform as RectTransform;
            if (rootRect == null)
                return;

            EnsureSpotlight();
            root.transform.SetAsLastSibling();
            spotlightRoot.gameObject.SetActive(true);
            spotlightRoot.SetAsFirstSibling();
            SetRootGraphicAlpha(0f);
            SetRootGraphicRaycast(false);

            var minScreen = new Vector2(screenRect.xMin - padding, screenRect.yMin - padding);
            var maxScreen = new Vector2(screenRect.xMax + padding, screenRect.yMax + padding);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, minScreen, null, out var minLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, maxScreen, null, out var maxLocal);

            var rootSize = rootRect.rect.size;
            var left = Mathf.Clamp(minLocal.x + rootSize.x * 0.5f, 0f, rootSize.x);
            var right = Mathf.Clamp(maxLocal.x + rootSize.x * 0.5f, 0f, rootSize.x);
            var bottom = Mathf.Clamp(minLocal.y + rootSize.y * 0.5f, 0f, rootSize.y);
            var top = Mathf.Clamp(maxLocal.y + rootSize.y * 0.5f, 0f, rootSize.y);

            SetSpotlightRect(spotlightRects[0], 0f, top, rootSize.x, rootSize.y - top);
            SetSpotlightRect(spotlightRects[1], 0f, 0f, rootSize.x, bottom);
            SetSpotlightRect(spotlightRects[2], 0f, bottom, left, top - bottom);
            SetSpotlightRect(spotlightRects[3], right, bottom, rootSize.x - right, top - bottom);
        }

        public void HideSpotlight()
        {
            if (spotlightRoot != null)
                spotlightRoot.gameObject.SetActive(false);

            RestoreRootGraphicColor();
        }

        private string BuildBodyText(DialogueDisplayData data)
        {
            if (!data.HasChoices || string.IsNullOrWhiteSpace(choiceLeadText))
                return data.Text;

            return $"{data.Text}\n\n<size=92%><color=#B8F0FF>{choiceLeadText}</color></size>";
        }

        private void HandleCloseClicked()
        {
            runner?.Stop();
        }

        private void BindButtonListeners()
        {
            UnbindButtonListeners();

            boundNextButton = nextButton;
            boundNextButton?.onClick.AddListener(HandleNextClicked);
            boundCloseButton = closeButton;
            boundCloseButton?.onClick.AddListener(HandleCloseClicked);
        }

        private void UnbindButtonListeners()
        {
            boundNextButton?.onClick.RemoveListener(HandleNextClicked);
            boundCloseButton?.onClick.RemoveListener(HandleCloseClicked);
            boundNextButton = null;
            boundCloseButton = null;
        }

        private void BuildChoices(IReadOnlyList<DialogueChoice> choices)
        {
            ClearChoices();

            if (choices == null || choices.Count == 0 || choiceRoot == null || choiceButtonPrefab == null)
            {
                if (choiceRoot != null)
                    choiceRoot.gameObject.SetActive(false);
                return;
                
            }

            choiceRoot.gameObject.SetActive(true);
            var layoutGroup = choiceRoot.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
                layoutGroup.enabled = false;

            // 선택지 묶음을 상자 아래쪽에 붙인다.
            //
            // 버튼은 상자 위에서부터 쌓는데 상자 높이는 씬에 고정(168)이라, 선택지가 하나뿐이면
            // 아래로 80이 비어 버튼이 대사 상자 한가운데 떠 있는 것처럼 보였다. 튜토리얼의 마지막
            // 줄이 정확히 그 경우다.
            //
            // 상자를 키우는 방법은 쓸 수 없다 — 아래를 기준으로 자라 올라가는데 본문 글상자 밑단이
            // 9밖에 안 떨어져 있어 곧장 글을 덮는다. 그래서 상자는 두고 남는 만큼 통째로 내린다.
            var contentHeight = choices.Count * choiceButtonHeight
                                + Mathf.Max(0, choices.Count - 1) * choiceButtonSpacing;
            var slack = Mathf.Max(0f, choiceRoot.rect.height - contentHeight - choiceTopPadding);

            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var index = i;
                var button = Instantiate(choiceButtonPrefab, choiceRoot);
                var choiceButtonView = button.GetComponent<DialogueChoiceButtonView>();
                var canSelect = runner == null || runner.CanSelect(choice);
                var buttonRect = button.transform as RectTransform;
                var targetPosition = new Vector2(0f, -slack - i * (choiceButtonHeight + choiceButtonSpacing));

                if (buttonRect != null)
                {
                    buttonRect.anchorMin = new Vector2(0f, 1f);
                    buttonRect.anchorMax = new Vector2(1f, 1f);
                    buttonRect.pivot = new Vector2(0.5f, 1f);
                    buttonRect.anchoredPosition = targetPosition;
                    buttonRect.sizeDelta = new Vector2(-choiceSidePadding * 2f, choiceButtonHeight);
                }

                choiceButtonView?.SetRestPosition(targetPosition);
                button.gameObject.SetActive(true);
                button.interactable = canSelect;

                if (canSelect)
                    button.onClick.AddListener(() => HandleChoiceClicked(index));

                if (choiceButtonView != null)
                {
                    choiceButtonView.Bind(choice, canSelect);
                }
                else
                {
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                        label.text = choice.Text;
                }

                choiceButtons.Add(button);
            }

            for (var i = 0; i < choiceButtons.Count; i++)
            {
                var button = choiceButtons[i];
                var rect = button.transform as RectTransform;
                if (rect != null && gameObject.activeInHierarchy)
                    choiceAnimations.Add(StartCoroutine(SlideChoiceIn(button, rect, i * choiceStaggerDelay)));
            }
        }

        private void ClearChoices()
        {
            for (var i = 0; i < choiceAnimations.Count; i++)
            {
                if (choiceAnimations[i] != null)
                    StopCoroutine(choiceAnimations[i]);
            }

            choiceAnimations.Clear();

            for (var i = 0; i < choiceButtons.Count; i++)
            {
                var button = choiceButtons[i];
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(button.gameObject);
                else
                    DestroyImmediate(button.gameObject);
            }

            choiceButtons.Clear();

            if (choiceRoot != null)
                choiceRoot.gameObject.SetActive(false);
        }

        private void HandleChoiceClicked(int choiceIndex)
        {
            runner?.SelectChoice(choiceIndex);
        }

        private void EnsureSpotlight()
        {
            if (spotlightRoot != null)
                return;

            if (rootRect == null)
            {
                Debug.LogError("Tutorial spotlight requires a dialogue root RectTransform.", this);
                return;
            }

            var view = sceneSpotlightView;
            if (view == null)
            {
                Debug.LogError("Tutorial spotlight requires a scene-placed TutorialSpotlightView.", this);
                return;
            }

            view.gameObject.SetActive(true);
            spotlightRoot = view.transform as RectTransform;
            var panels = view.DimPanels;
            for (var i = 0; i < spotlightRects.Length; i++)
            {
                spotlightRects[i] = panels != null && i < panels.Length ? panels[i] : null;
                var image = spotlightRects[i] != null ? spotlightRects[i].GetComponent<Image>() : null;
                if (image != null)
                    image.color = spotlightDimColor;
            }

            spotlightRoot.gameObject.SetActive(false);
        }

        private void CaptureRootGraphicColor()
        {
            if (rootGraphic == null || hasRootGraphicDefaultColor)
                return;

            rootGraphicDefaultColor = rootGraphic.color;
            rootGraphicDefaultRaycastTarget = rootGraphic.raycastTarget;
            hasRootGraphicDefaultColor = true;
        }

        private void SetRootGraphicAlpha(float alpha)
        {
            if (rootGraphic == null)
                return;

            CaptureRootGraphicColor();
            var color = rootGraphicDefaultColor;
            color.a = alpha;
            rootGraphic.color = color;
        }

        private void RestoreRootGraphicColor()
        {
            if (rootGraphic != null && hasRootGraphicDefaultColor)
            {
                rootGraphic.color = rootGraphicDefaultColor;
                rootGraphic.raycastTarget = rootGraphicDefaultRaycastTarget;
            }
        }

        private void SetRootGraphicRaycast(bool raycastTarget)
        {
            if (rootGraphic == null)
                return;

            CaptureRootGraphicColor();
            rootGraphic.raycastTarget = raycastTarget;
        }

        private void SetSpotlightRect(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        private IEnumerator SlideChoiceIn(Button choiceButton, RectTransform choiceRect, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            var target = choiceRect.anchoredPosition;
            var start = target + new Vector2(choiceSlideOffset, 0f);
            var elapsed = 0f;

            choiceRect.anchoredPosition = start;

            while (elapsed < choiceSlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = choiceSlideDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / choiceSlideDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                choiceRect.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
                yield return null;
            }

            choiceRect.anchoredPosition = target;
            var choiceButtonView = choiceButton.GetComponent<DialogueChoiceButtonView>();
            choiceButtonView?.SetRestPosition(target);
        }
    }
}
