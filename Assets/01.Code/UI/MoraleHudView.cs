using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class MoraleHudView : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO managementEventChannel;
        [SerializeField] private TMP_Text moraleText;
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
            NestHudStyle.ApplyPanel(gameObject);
            NestHudStyle.ApplyTopRightCard(gameObject, moraleText, 3, new Color(0.3f, 0.9f, 0.62f, 1f));
            if (moraleText == null)
                return;
            _baseMoraleColor = moraleText.color;
            _baseMoraleScale = moraleText.transform.localScale;
        }

        private void OnEnable()
        {
            managementEventChannel?.AddListener<MoraleChangedEvent>(HandleMoraleChanged);
            openButton?.onClick.AddListener(ShowDetail);
            closeButton?.onClick.AddListener(HideDetail);
            HideDetail();
        }

        private void OnDisable()
        {
            managementEventChannel?.RemoveListener<MoraleChangedEvent>(HandleMoraleChanged);
            openButton?.onClick.RemoveListener(ShowDetail);
            closeButton?.onClick.RemoveListener(HideDetail);
            ResetMoraleVisual();
        }

        private void HandleMoraleChanged(MoraleChangedEvent evt)
        {
            currentMorale = evt.CurrentMorale;

            if (moraleText != null)
            {
                moraleText.text = string.Format(moraleFormat, evt.CurrentMorale);
                PlayMoraleFeedback(evt.Delta);
            }

            AddHistory(evt);
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

        private void AddHistory(MoraleChangedEvent evt)
        {
            var sign = evt.Delta > 0 ? "+" : string.Empty;
            var reason = string.IsNullOrWhiteSpace(evt.Reason) ? "변화" : evt.Reason;
            historyLines.Insert(0, $"{reason}: {sign}{evt.Delta}");

            while (historyLines.Count > maxHistoryCount)
                historyLines.RemoveAt(historyLines.Count - 1);
        }

        private void RefreshDetail()
        {
            if (detailText == null)
                return;

            if (historyLines.Count == 0)
            {
                detailText.text = $"{detailTitle}\n민심: {currentMorale}\n\n최근 변화 없음";
                return;
            }

            detailText.text = $"{detailTitle}\n민심: {currentMorale}\n\n최근 변화\n{string.Join("\n", historyLines)}";
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
