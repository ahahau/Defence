using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 한 번에 하나만 떠 있는 창.
    ///
    /// 창들이 서로를 모른 채 각자 켜지고 꺼졌다. 정렬 순서도 없어서 둘이 동시에 열리면
    /// 형제 순서대로 그냥 겹쳐 그려졌고, 아래에 깔린 창은 눌리지도 않았다.
    ///
    /// 여는 쪽을 고치려면 창마다 "다른 창이 떠 있나"를 묻게 해야 하는데, 창이 열 개가 넘어
    /// 서로를 다 알게 되면 하나 추가할 때마다 열 곳을 고쳐야 한다. 그래서 반대로 뒤집었다 —
    /// 켜지는 쪽이 스스로 나머지를 내린다. 창은 서로를 몰라도 되고, 새 창은 이 컴포넌트를
    /// 붙이기만 하면 규칙에 들어온다.
    ///
    /// 붙이는 자리는 창의 루트다. 패널 클래스는 대개 <c>panelRoot</c>를 켜고 끄므로
    /// 그 오브젝트에 붙어야 열림·닫힘과 생명주기가 맞는다.
    ///
    /// 강제로 떠야 하는 창(정책 선택처럼 고르기 전에는 못 넘어가는 것)에는 붙이지 않는다.
    /// 그런 창까지 이 규칙에 넣으면 다른 창을 여는 것만으로 결정을 건너뛸 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExclusiveWindow : MonoBehaviour
    {
        private static readonly List<ExclusiveWindow> Open = new();

        private void OnEnable()
        {
            for (var i = Open.Count - 1; i >= 0; i--)
            {
                var other = Open[i];
                if (other == null)
                {
                    Open.RemoveAt(i);
                    continue;
                }

                if (other == this)
                    continue;

                other.gameObject.SetActive(false);
            }

            if (!Open.Contains(this))
                Open.Add(this);
        }

        private void OnDisable()
        {
            Open.Remove(this);
        }

        /// <summary>지금 떠 있는 창을 모두 내린다. 습격이 시작될 때처럼 판이 바뀌는 순간에 쓴다.</summary>
        public static void CloseAll()
        {
            for (var i = Open.Count - 1; i >= 0; i--)
            {
                var window = Open[i];
                if (window != null)
                    window.gameObject.SetActive(false);
            }

            Open.Clear();
        }
    }
}
