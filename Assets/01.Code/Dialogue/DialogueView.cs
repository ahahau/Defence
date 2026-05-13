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
        
        private DialogueRunner runner;
        private Button boundNextButton;
        private Button boundCloseButton;

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
            Button close)
        {
            UnbindButtonListeners();

            root = rootObject != null ? rootObject : gameObject;
            titleText = title;
            speakerText = speaker;
            bodyText = body;
            progressText = progress;
            nextButton = next;
            closeButton = close;

            if (isActiveAndEnabled)
                BindButtonListeners();
        }

        public void Show(DialogueSequenceSO sequence, int lineIndex, DialogueLine line)
        {
            if (root == null)
                root = gameObject;

            root.SetActive(true);
            SetText(titleText, sequence != null ? sequence.DisplayName : string.Empty);
            SetText(speakerText, line.SpeakerName);
            SetText(bodyText, line.Text);
            SetText(progressText, sequence != null ? $"{lineIndex + 1}/{sequence.LineCount}" : string.Empty);
        }

        public void Hide()
        {
            if (root == null)
                root = gameObject;

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
            if (boundNextButton == nextButton && boundCloseButton == closeButton)
                return;

            UnbindButtonListeners();

            boundNextButton = nextButton;
            boundCloseButton = closeButton;
            boundNextButton?.onClick.AddListener(HandleNextClicked);
            boundCloseButton?.onClick.AddListener(HandleCloseClicked);
        }

        private void UnbindButtonListeners()
        {
            boundNextButton?.onClick.RemoveListener(HandleNextClicked);
            boundCloseButton?.onClick.RemoveListener(HandleCloseClicked);
            boundNextButton = null;
            boundCloseButton = null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
