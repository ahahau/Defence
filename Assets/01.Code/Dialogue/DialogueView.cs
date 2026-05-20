using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Dialogue
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
        [SerializeField, Min(0f)] private float choiceSlideDuration = 0.22f;
        [SerializeField] private float choiceSlideOffset = 280f;
        [SerializeField, Min(0f)] private float choiceStaggerDelay = 0.045f;
        [SerializeField, Min(0f)] private float choiceTopPadding = 14f;
        [SerializeField, Min(0f)] private float choiceSidePadding = 14f;
        [SerializeField, Min(1f)] private float choiceButtonHeight = 54f;
        [SerializeField, Min(0f)] private float choiceButtonSpacing = 10f;
        
        private DialogueRunner runner;
        private Button boundNextButton;
        private Button boundCloseButton;
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

            root.SetActive(true);
            BindButtonListeners();

            if (speakerText != null)
                speakerText.text = data.SpeakerName;

            if (bodyText != null)
                bodyText.text = data.Text;

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

        public void Hide()
        {
            if (root == null)
                root = gameObject;

            ClearChoices();
            root.SetActive(false);
        }

        private void HandleNextClicked()
        {
            runner?.Next();
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

            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var index = i;
                var button = Instantiate(choiceButtonPrefab, choiceRoot);
                var choiceButtonView = button.GetComponent<DialogueChoiceButtonView>();
                var canSelect = runner == null || runner.CanSelect(choice);
                var buttonRect = button.transform as RectTransform;
                var targetPosition = new Vector2(0f, -choiceTopPadding - i * (choiceButtonHeight + choiceButtonSpacing));

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
