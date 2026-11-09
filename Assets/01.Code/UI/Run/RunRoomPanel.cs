using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 단면도 방 하나 위에 붙는 정보 띠(이름·상태·내구·마력 구슬)와 아래쪽 적 행동 예고.
    /// 값은 디렉터가 정해 넘기고, 구슬 클릭만 밖으로 알린다.
    /// </summary>
    public class RunRoomPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform header;
        [SerializeField] private RectTransform footer;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text status;
        [SerializeField] private UnityEngine.UI.Image durabilityFill;
        [SerializeField] private UnityEngine.UI.Image[] manaPips = Array.Empty<UnityEngine.UI.Image>();
        [SerializeField] private UnityEngine.UI.Button[] manaButtons = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] private TMP_Text intent;

        private UnityEngine.Events.UnityAction[] _pipActions;

        /// <summary>구슬 번호(0부터). 디렉터가 마력을 바꾼다.</summary>
        public event Action<int> PipClicked;

        public RectTransform Header => header;
        public RectTransform Footer => footer;

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (int i = 0; i < manaButtons.Length; i++)
                if (manaButtons[i] != null)
                    manaButtons[i].onClick.AddListener(_pipActions[i]);
        }

        private void OnDisable()
        {
            if (_pipActions == null) return;
            for (int i = 0; i < manaButtons.Length; i++)
                if (manaButtons[i] != null)
                    manaButtons[i].onClick.RemoveListener(_pipActions[i]);
        }

        private void EnsureRuntimeState()
        {
            if (_pipActions != null && _pipActions.Length == manaButtons.Length) return;
            _pipActions = new UnityEngine.Events.UnityAction[manaButtons.Length];
            for (int i = 0; i < manaButtons.Length; i++)
            {
                int pip = i;
                _pipActions[i] = () => PipClicked?.Invoke(pip);
            }
        }

        /// <summary>방 정보를 그린다. maxMana만큼 구슬을 보이고 mana만큼 채운다.</summary>
        public void Show(string titleText, string statusText, Color statusColor, float durability, bool showDurability,
            int mana, int maxMana)
        {
            if (title != null) title.text = titleText;
            if (status != null)
            {
                status.text = statusText;
                status.color = statusColor;
            }
            if (durabilityFill != null)
            {
                durabilityFill.transform.parent.gameObject.SetActive(showDurability);
                durabilityFill.fillAmount = durability;
                durabilityFill.color = durability >= 1f ? HudPalette.Gain : durability <= 0f ? HudPalette.Danger : HudPalette.Warn;
            }
            for (int i = 0; i < manaPips.Length; i++)
            {
                if (manaPips[i] == null) continue;
                manaPips[i].gameObject.SetActive(i < maxMana);
                manaPips[i].color = i < mana ? HudPalette.Magic : HudPalette.Track;
            }
        }

        public void ShowIntent(string text)
        {
            if (intent != null)
                intent.text = text;
        }
    }
}
