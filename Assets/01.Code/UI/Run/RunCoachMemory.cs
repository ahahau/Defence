using System;
using Code.Run;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 첫 판 안내를 한 번 해 봤는지 기억한다. 런을 넘어 남아야 해서 음량 설정처럼 PlayerPrefs에 둔다
    /// (2026-11-12 사용자 선택 "한 번 해 보면 끝"). 시연 저장과는 따로다.
    /// </summary>
    public sealed class RunCoachMemory
    {
        private const string KeyPrefix = "run.coach.";
        private readonly bool[] _done;

        public RunCoachMemory()
        {
            Array steps = Enum.GetValues(typeof(RunCoachStep));
            _done = new bool[steps.Length];
            foreach (RunCoachStep step in steps)
                _done[(int)step] = step == RunCoachStep.None || PlayerPrefs.GetInt(KeyPrefix + step, 0) == 1;
        }

        public bool IsDone(RunCoachStep step) => (int)step < 0 || (int)step >= _done.Length || _done[(int)step];

        public void MarkDone(RunCoachStep step)
        {
            if (IsDone(step)) return;
            _done[(int)step] = true;
            PlayerPrefs.SetInt(KeyPrefix + step, 1);
            PlayerPrefs.Save();
        }
    }
}
