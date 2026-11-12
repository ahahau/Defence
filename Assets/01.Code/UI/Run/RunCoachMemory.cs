using System;
using Code.Run;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 첫 판 안내를 한 번 해 봤는지 기억한다. 런을 넘어 남아야 해서 음량 설정처럼 PlayerPrefs에 둔다
    /// (2026-11-12 사용자 선택 "한 번 해 보면 끝"). 시연 저장과는 따로다.
    /// 설정 창의 "첫 판 안내 다시 보기"가 전투 중에도 바로 듣도록 기억해 두지 않고 매번 읽는다.
    /// </summary>
    public sealed class RunCoachMemory
    {
        private const string KeyPrefix = "run.coach.";

        public bool IsDone(RunCoachStep step) => step == RunCoachStep.None || PlayerPrefs.GetInt(KeyPrefix + step, 0) == 1;

        public void MarkDone(RunCoachStep step)
        {
            if (IsDone(step)) return;
            PlayerPrefs.SetInt(KeyPrefix + step, 1);
            PlayerPrefs.Save();
        }

        /// <summary>모든 안내를 처음처럼 다시 보이게 한다(설정 창 "첫 판 안내 다시 보기").</summary>
        public static void ResetAll()
        {
            foreach (RunCoachStep step in Enum.GetValues(typeof(RunCoachStep))) PlayerPrefs.DeleteKey(KeyPrefix + step);
            PlayerPrefs.Save();
        }
    }
}
