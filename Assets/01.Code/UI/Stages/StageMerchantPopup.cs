using System.Collections.Generic;
using System.Text;
using Code.Stages;
using Code.UI.Popup;

namespace Code.UI.Stages
{
    /// <summary>
    /// 스테이지 상인 팝업(2026-11-02 사용자 결정: 매 준비 단계 HUD 버튼, 범용 팝업에 물건 3개).
    /// 범용 팝업으로 띄우기만 하고 구매는 <see cref="StageSession.TryBuyMerchantItem"/>가 다시 검사한 뒤 적용한다.
    /// </summary>
    public sealed class StageMerchantPopup
    {
        private const string Tag = "stage-merchant";
        private readonly StageSession _session;

        public StageMerchantPopup(StageSession session) => _session = session;

        public void Tick(UiFrameButton merchantButton)
        {
            if (merchantButton != null)
                merchantButton.SetVisible(StageMerchant.IsOpen(_session));
        }

        public void Show()
        {
            if (!StageMerchant.IsOpen(_session) || PopupController.IsOpen)
                return;
            IReadOnlyList<StageMerchantItem> items = StageMerchant.ItemsOf(_session.Catalog);
            int attempt = _session.Merchant.Attempt;
            int wave = _session.WaveIndex;
            // 팝업 본문은 508×104(17pt, 넘치면 잘림)라 인사 한 줄에 물건마다 효과 한 줄(29자 안쪽)만 붙인다
            StringBuilder message = new("임프 상인: \"이번 스테이지에만 듣는 물건이오.\"");
            List<PopupButtonSpec> options = new();
            // 물건 수·빈 칸은 스테이지 시작 때 StageMerchant.TryValidate가 막는다
            for (int i = 0; i < items.Count; i++)
            {
                StageMerchantItem item = items[i];
                int index = i;
                string blocked = Blocked(item);
                message.Append('\n').Append(item.Label).Append(": ").Append(EffectText(item));
                if (blocked.Length > 0)
                    message.Append(" (").Append(blocked).Append(')');
                options.Add(new PopupButtonSpec($"{item.Label} {item.Price}G", PopupButtonStyle.Primary,
                    () => Buy(index, attempt, wave), blocked.Length == 0));
            }
            // 닫기(ESC)는 아무것도 사지 않는다
            PopupController.Show(PopupRequest.Choice("상인", message.ToString(), options, () => { }).WithTag(Tag));
        }

        /// <summary>예: "코어 7→10/10". 보수는 최대 체력에서 잘린 실제 값을 보인다.</summary>
        private string EffectText(StageMerchantItem item)
        {
            // 가득 차면 버튼 옆 잠김 이유("코어 가득 참")가 알려 주므로 효과만 보인다
            return item.CoreRepair > 0 && StageCoreRepairPreview.TryText(_session, item.CoreRepair, out string text)
                ? text : item.EffectText();
        }

        private string Blocked(StageMerchantItem item)
        {
            // 저장이 막혀 있으면 세션이 팔지 않으므로 누르기 전에 잠가 둔다
            if (!string.IsNullOrEmpty(_session.SaveError))
                return "저장 오류";
            if (_session.Merchant.IsSoldOut(item, _session.WaveIndex))
                return "구매함";
            if (item.CoreRepair > 0 && _session.CoreHealth >= _session.MaxCoreHealth)
                return "코어 가득 참";
            return _session.Gold < item.Price ? "금화 부족" : string.Empty;
        }

        private void Buy(int index, int attempt, int wave)
        {
            // 씬을 떠났거나 재도전·다음 준비로 넘어간 뒤의 콜백은 세션이 다시 걸러 낸다
            if (_session != null)
                _session.TryBuyMerchantItem(index, attempt, wave);
        }
    }
}
