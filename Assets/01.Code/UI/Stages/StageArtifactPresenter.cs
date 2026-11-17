using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Code.Artifacts;
using Code.Stages;

namespace Code.UI.Stages
{
    /// <summary>
    /// 유물 칸·유물 창의 문구. 조합 효과는 스테이지에서 대상 구분이 없으므로 원래 설명 대신 실제 능력치로 적는다.
    /// </summary>
    internal sealed class StageArtifactPresenter
    {
        private readonly StringBuilder _text = new();
        private readonly List<ArtifactCombo> _combos = new();

        /// <summary>편성 패널 두 줄: 장착 이름과 성립한 조합.</summary>
        internal string Summary(StageSession session)
        {
            _text.Clear();
            _text.Append("유물 ").Append(session.Artifacts.Equipped.Count).Append('/').Append(session.Catalog.ArtifactSlots);
            if (session.Artifacts.Equipped.Count == 0)
                _text.Append(" · 장착 없음");
            for (int i = 0; i < session.Artifacts.Equipped.Count; i++)
                _text.Append(i == 0 ? ": " : " · ").Append(session.Artifacts.Equipped[i].Label);
            session.Artifacts.CollectCombos(_combos);
            _text.Append("\n조합: ");
            if (_combos.Count == 0)
                _text.Append("없음");
            for (int i = 0; i < _combos.Count; i++)
                _text.Append(i == 0 ? string.Empty : " · ").Append(_combos[i].DisplayName);
            return _text.ToString();
        }

        /// <summary>유물 창 한 줄: 이름, 효과, 함께 장착하면 성립하는 조합.</summary>
        internal string Row(StageArtifact artifact, StageCatalogSO catalog)
        {
            _text.Clear();
            _text.Append("<b>").Append(artifact.Label).Append("</b>\n").Append(artifact.Description);
            if (catalog.ArtifactCombos != null)
                catalog.ArtifactCombos.CollectCombosWith(artifact.Artifact, _combos);
            else
                _combos.Clear();
            for (int i = 0; i < _combos.Count; i++)
            {
                ArtifactDataSO partner = _combos[i].GetPartnerOf(artifact.Artifact);
                _text.Append(i == 0 ? "\n조합: " : " / ").Append(partner != null ? partner.DisplayName : "?")
                    .Append(" → ").Append(_combos[i].DisplayName).Append('(');
                AppendEffect(_combos[i].StatBonus);
                _text.Append(')');
            }
            return _text.ToString();
        }

        private void AppendEffect(ArtifactStatBonus bonus)
        {
            int start = _text.Length;
            if (bonus.AttackDamage != 0)
                Part(start).Append("공격력 ").Append(bonus.AttackDamage.ToString("+0;-0", CultureInfo.InvariantCulture));
            if (bonus.AttackDamageMultiplier != 1f)
                Part(start).Append("공격력 ×").Append(bonus.AttackDamageMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
            if (bonus.MaxHealth != 0)
                Part(start).Append("최대 체력 ").Append(bonus.MaxHealth.ToString("+0;-0", CultureInfo.InvariantCulture));
            if (bonus.AttackIntervalMultiplier != 1f)
                Part(start).Append("공격 간격 ×").Append(bonus.AttackIntervalMultiplier.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private StringBuilder Part(int start) => _text.Length > start ? _text.Append(", ") : _text;
    }
}
