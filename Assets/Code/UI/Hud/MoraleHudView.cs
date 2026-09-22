using System.Collections.Generic;
using Code.Manager;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class MoraleHudView : MonoBehaviour
    {
        [SerializeField] private MoralePolicyManager moralePolicyManager;
        [SerializeField] private TMP_Text moraleText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button closeButton;
        [SerializeField] private string moraleFormat = "민심 {0}";
        [SerializeField] private string detailTitle = "민심 현황";
        [SerializeField, Min(1)] private int maxHistoryCount = 8;

        private readonly List<string> historyLines = new();
        private int currentMorale;
        private Color _baseMoraleColor = Color.white;
        private Vector3 _baseMoraleScale = Vector3.one;

        private void Awake()
        {
            if (moraleText == null)
                return;
            _baseMoraleColor = moraleText.color;
            _baseMoraleScale = moraleText.transform.localScale;
        }

        private void OnEnable()
        {
            if (moralePolicyManager != null)
            {
                moralePolicyManager.MoraleChanged += HandleMoraleChanged;
                HandleMoraleChanged(moralePolicyManager.CurrentMorale, 0, string.Empty);
            }
            openButton?.onClick.AddListener(ShowDetail);
            closeButton?.onClick.AddListener(HideDetail);
            HideDetail();
        }

        private void OnDisable()
        {
            if (moralePolicyManager != null)
                moralePolicyManager.MoraleChanged -= HandleMoraleChanged;
            openButton?.onClick.RemoveListener(ShowDetail);
            closeButton?.onClick.RemoveListener(HideDetail);
            ResetMoraleVisual();
        }

        private void HandleMoraleChanged(int morale, int delta, string reason)
        {
            Render(new MoraleHudState(morale, delta, reason));
        }

        public void Render(MoraleHudState state)
        {
            currentMorale = state.Value;

            if (moraleText != null)
            {
                moraleText.text = string.Format(moraleFormat, state.Value);
                PlayMoraleFeedback(state.Delta);
            }

            AddHistory(state.Delta, state.Reason);
            RefreshDetail();
        }

        private void ShowDetail()
        {
            if (detailRoot == null)
                return;

            RefreshDetail();
            detailRoot.SetActive(true);
            detailRoot.transform.SetAsLastSibling();
        }

        private void HideDetail()
        {
            if (detailRoot != null)
                detailRoot.SetActive(false);
        }

        private void AddHistory(int delta, string reason)
        {
            var sign = delta > 0 ? "+" : string.Empty;
            var label = string.IsNullOrWhiteSpace(reason) ? "변화" : reason;
            historyLines.Insert(0, $"{label}: {sign}{delta}");

            while (historyLines.Count > maxHistoryCount)
                historyLines.RemoveAt(historyLines.Count - 1);
        }

        private void RefreshDetail()
        {
            if (detailText == null)
                return;

            var header = $"{detailTitle}\n민심: {currentMorale}{BuildEffectText()}";

            if (historyLines.Count == 0)
            {
                detailText.text = $"{header}\n\n최근 변화 없음";
                return;
            }

            detailText.text = $"{header}\n\n최근 변화\n{string.Join("\n", historyLines)}";
        }

        /// <summary>
        /// 민심이 지금 무엇을 하고 있는지. 숫자만 띄우면 올릴 이유도 내릴 이유도 보이지 않는다.
        /// </summary>
        private static string BuildEffectText()
        {
            var morale = Code.Manager.MoralePolicyManager.Current;
            if (morale == null)
                return string.Empty;

            var upkeep = morale.UpkeepMultiplier;
            var upkeepLine = Mathf.Approximately(upkeep, 1f)
                ? "유지비 평상시"
                : upkeep > 1f
                    ? $"<color=#FF7A6B>유지비 +{Mathf.RoundToInt((upkeep - 1f) * 100f)}%</color>"
                    : $"<color=#7ADB8A>유지비 -{Mathf.RoundToInt((1f - upkeep) * 100f)}%</color>";

            return $"\n<size=85%>{upkeepLine}  ·  지원자 {morale.AdjustRecruitCount(100)}%</size>";
        }

        private void PlayMoraleFeedback(int delta)
        {
            if (moraleText == null || delta == 0)
                return;

            moraleText.DOKill();
            moraleText.transform.DOKill();
            moraleText.color = delta > 0
                ? new Color(0.38f, 1f, 0.56f, 1f)
                : new Color(1f, 0.3f, 0.28f, 1f);
            moraleText.DOColor(_baseMoraleColor, 0.58f).SetUpdate(true).SetLink(moraleText.gameObject);
            moraleText.transform.localScale = _baseMoraleScale;
            moraleText.transform.DOPunchScale(Vector3.one * 0.14f, 0.3f, 7, 0.7f)
                .SetUpdate(true).SetLink(moraleText.gameObject);
        }

        private void ResetMoraleVisual()
        {
            if (moraleText == null)
                return;
            moraleText.DOKill();
            moraleText.transform.DOKill();
            moraleText.color = _baseMoraleColor;
            moraleText.transform.localScale = _baseMoraleScale;
        }
    }
}
