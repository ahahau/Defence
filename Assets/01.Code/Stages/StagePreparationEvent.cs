using System;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지 준비 단계의 짧은 방어 준비 사건(2026-11-02 사용자 승인, StagePreparationContentDesign.md).
    /// 선택지 순서가 준비 저장의 선택 번호(1부터)이므로 이미 쓴 사건의 선택지 순서를 바꾸지 않는다.
    /// </summary>
    [Serializable]
    public sealed class StagePreparationEvent
    {
        /// <summary>범용 팝업 버튼 수 상한과 같다.</summary>
        public const int MaxOptions = 3;

        [field: SerializeField] public string Title { get; private set; }
        [field: SerializeField, Tooltip("임프 참모 대사 한 줄. 팝업 본문 첫 줄이다.")] public string Line { get; private set; }
        [field: SerializeField, Tooltip("다시 열기 버튼 글자(짧게).")] public string ButtonLabel { get; private set; }
        [field: SerializeField, Min(0), Tooltip("사건을 여는 준비 단계(0부터). 1이면 첫 웨이브를 막은 뒤다.")]
        public int Wave { get; private set; } = 1;
        [field: SerializeField] public StagePreparationOption[] Options { get; private set; } = Array.Empty<StagePreparationOption>();

        public bool IsDefined => Options != null && Options.Length > 0;

        // 직렬화 데이터가 없는 에셋에서 만들 때도 필드 초기값(둘째 웨이브·빈 선택지)이 적용되게 둔다
        private StagePreparationEvent()
        {
        }

        public StagePreparationEvent(string title, string line, string buttonLabel, int wave, StagePreparationOption[] options)
        {
            Title = title;
            Line = line;
            ButtonLabel = buttonLabel;
            Wave = wave;
            Options = options ?? Array.Empty<StagePreparationOption>();
        }

        /// <summary>스테이지 검사용. 사건이 없으면 통과한다.</summary>
        public bool TryValidate(int waveCount, out string reason)
        {
            reason = string.Empty;
            if (!IsDefined)
                return true;
            if (Options.Length > MaxOptions)
                reason = $"준비 사건 선택지는 {MaxOptions}개까지입니다";
            else if (Wave < 0 || Wave >= waveCount)
                reason = "준비 사건 웨이브가 범위 밖입니다";
            else
                foreach (StagePreparationOption option in Options)
                    if (option == null || string.IsNullOrEmpty(option.Label) || option.CoreRepair < 0)
                        reason = "준비 사건 선택지가 비었거나 잘못됐습니다";
            return string.IsNullOrEmpty(reason);
        }
    }
}
