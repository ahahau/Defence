using UnityEngine;

namespace Code.Units
{
    /// <summary>
    /// 명령마다 다른 재사용 대기시간과 대가.
    ///
    /// 전에는 무엇을 내리든 3초 하나였다. 그러면 명령이 결정이 아니라 토글이 된다 —
    /// 상황이 바뀔 때마다 전원을 돌격으로 돌렸다가 경계로 되돌리면 그만이었다.
    ///
    /// 대가는 던전 권능 게이지로 받는다. 웨이브 중에만 도는 자원이라,
    /// "권능을 쏟을까, 진형을 바꿀까"가 한 주머니를 두고 다투게 된다.
    /// 대기 단계에서는 받지 않는다 — 그때는 게이지가 비어 있고,
    /// 애초에 준비 단계는 자유롭게 짜라고 있는 시간이다.
    /// </summary>
    public static class UnitCommandRules
    {
        /// <summary>명령을 내린 뒤 이 유닛에게 다시 명령하기까지의 시간(초).</summary>
        public static float GetCooldown(UnitCommand command) => command switch
        {
            // 되돌리기는 싸야 한다. 잘못 누른 것이 벌이 되면 명령 자체를 안 쓰게 된다.
            UnitCommand.Standby => 2f,
            UnitCommand.Guard => 5f,
            UnitCommand.Assault => 6f,
            // 전투에서 빼는 결정이라 가장 무겁다.
            UnitCommand.Rest => 8f,
            _ => 3f
        };

        public static string GetLabel(UnitCommand command) => command switch
        {
            UnitCommand.Standby => "대기",
            UnitCommand.Guard => "경계",
            UnitCommand.Assault => "공격",
            UnitCommand.Rest => "휴식",
            _ => "명령"
        };

        /// <summary>버튼에 붙일 대가 표시. 명령을 막는 것은 재사용 대기뿐이다.</summary>
        public static string DescribeCost(UnitCommand command) =>
            $"재사용 {GetCooldown(command):0.#}초";
    }
}
