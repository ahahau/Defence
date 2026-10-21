using Code.Manager;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 고용 창(uGUI, 화면 가운데). 도크의 고용 버튼으로 열고, 지원자 목록은 정산표 "유닛 상점"과 같은 칸(<see cref="SettlementShopPane"/>)을 쓴다.
    /// 영업 전 대기 중에만 열리고, 영업이 시작되면 닫는다. 고용 판단·결제는 <see cref="HiredUnitRoster"/>가 한다.
    /// 예전에는 고용이 청산일 정산표에서만 돼 첫 주(1~6일차)에 부하를 둘 방법이 없었다.
    /// </summary>
    public sealed class RecruitScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private SettlementShopPane shop;
        [SerializeField] private UiFrameButton closeButton;

        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>대기 중이 아니면 열지 않는다.</summary>
        public bool Open()
        {
            if (DayManager.Current == null || !DayManager.Current.IsStandby)
                return false;

            root.SetActive(true);
            shop.SetFeedback(string.Empty);
            shop.Refresh();
            shop.RewindScroll();
            return true;
        }

        public void Close()
        {
            if (root != null)
                root.SetActive(false);
        }

        private void Awake() => Close();

        private void OnEnable() => closeButton?.Button.onClick.AddListener(Close);

        private void OnDisable() => closeButton?.Button.onClick.RemoveListener(Close);

        private void Update()
        {
            if (IsOpen && (DayManager.Current == null || !DayManager.Current.IsStandby))
                Close();
        }
    }
}
