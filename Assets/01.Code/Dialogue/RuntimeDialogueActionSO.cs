using System;
using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>
    /// 코드로 만든 사건(상황 사건)의 선택지 결과. 에셋으로 저장하지 않고 그 자리에서 만들어 쓰고 버린다.
    /// 금액이 그때그때 달라 고정값 액션 에셋으로는 표현할 수 없어서 둔다.
    /// </summary>
    public sealed class RuntimeDialogueActionSO : DialogueActionSO
    {
        private Action _execute;

        public static RuntimeDialogueActionSO Create(Action execute)
        {
            var action = CreateInstance<RuntimeDialogueActionSO>();
            action.hideFlags = HideFlags.DontSave;
            action._execute = execute;
            return action;
        }

        public override void Execute(DialogueActionContext context) => _execute?.Invoke();
    }
}
