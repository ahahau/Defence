using System.Collections.Generic;
using System.Text;
using Code.Artifacts;
using Code.Manager;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 유물 줄과 설명 카드의 값을 만든다. 칸 수는 고정이고(유물 종류 수) 산 순서대로 찬다.
    /// 가진 유물로 성립한 조합(<see cref="ArtifactCombo.IsOwnedBy"/>)에 든 유물은 금색 테두리, 머리에 "조합 N 성립".
    /// </summary>
    public static class ArtifactRowPresenter
    {
        public const int SlotCount = 12;

        public static ArtifactRowViewModel Build(IReadOnlyList<ArtifactDataSO> owned, bool unlocked, ArtifactComboCatalogSO combos)
        {
            var model = new ArtifactRowViewModel { Visible = unlocked };
            var count = 0;
            if (owned != null)
            {
                foreach (var artifact in owned)
                {
                    if (artifact == null || artifact.IsConsumable || model.Slots.Count >= SlotCount)
                        continue;

                    count++;
                    model.Slots.Add(new ArtifactSlotViewModel
                    {
                        Artifact = artifact,
                        Icon = artifact.Icon,
                        IconColor = artifact.IconColor,
                        InCombo = InOwnedCombo(artifact, owned, combos)
                    });
                }
            }

            model.Header = $"유물 {count}";
            var formed = CountOwnedCombos(owned, combos);
            model.ComboSummary = formed > 0 ? $"조합 {formed} 성립" : string.Empty;
            model.EmptyHint = count == 0 ? "떠돌이 상인에게서 살 수 있습니다" : string.Empty;
            return model;
        }

        public static ArtifactDetailViewModel BuildDetail(ArtifactDataSO artifact, IReadOnlyList<ArtifactDataSO> owned, ArtifactComboCatalogSO combos)
        {
            var detail = new ArtifactDetailViewModel
            {
                Name = artifact.DisplayName,
                Target = TargetLabel(artifact.Target),
                Description = artifact.Description
            };

            var lines = new StringBuilder();
            if (combos != null && combos.Combos != null)
            {
                foreach (var combo in combos.Combos)
                {
                    var partner = combo?.GetPartnerOf(artifact);
                    if (partner == null)
                        continue;

                    if (lines.Length > 0)
                        lines.Append('\n');
                    if (owned != null && combo.IsOwnedBy(owned))
                    {
                        lines.Append(HudPalette.Paint($"조합 성립 · {combo.DisplayName}", HudPalette.Accent));
                        lines.Append('\n');
                        lines.Append(HudPalette.Paint($"{MoralePolicyManager.WithParticle(partner.DisplayName)} 함께", HudPalette.TextDim));
                    }
                    else
                    {
                        lines.Append(HudPalette.Paint($"{combo.DisplayName} · {partner.DisplayName} 필요", HudPalette.TextMuted));
                    }
                }
            }

            detail.Combos = lines.ToString();
            return detail;
        }

        public static string TargetLabel(ArtifactTarget target) => target switch
        {
            ArtifactTarget.HiredUnitsOnly => "고용 유닛",
            ArtifactTarget.PlayerOnly => "던전의 주인",
            _ => "모든 유닛"
        };

        private static bool InOwnedCombo(ArtifactDataSO artifact, IReadOnlyList<ArtifactDataSO> owned, ArtifactComboCatalogSO combos)
        {
            if (combos == null || combos.Combos == null)
                return false;

            foreach (var combo in combos.Combos)
            {
                if (combo != null && combo.GetPartnerOf(artifact) != null && combo.IsOwnedBy(owned))
                    return true;
            }

            return false;
        }

        private static int CountOwnedCombos(IReadOnlyList<ArtifactDataSO> owned, ArtifactComboCatalogSO combos)
        {
            if (owned == null || combos == null || combos.Combos == null)
                return 0;

            var count = 0;
            foreach (var combo in combos.Combos)
            {
                if (combo != null && combo.IsOwnedBy(owned))
                    count++;
            }

            return count;
        }
    }
}
