using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// TMP 텍스트가 세로로 흐르거나 줄바꿈으로 카드 높이를 밀어내지 않게 잡아 주는 보정.
    /// 레이아웃 그룹이 회전·스케일을 건드린 뒤에도 한 줄을 유지시킨다.
    /// </summary>
    internal static class TmpTextLayoutUtility
    {
        public static void KeepHorizontal(TMP_Text text, bool replaceLineBreaks = false)
        {
            if (text == null)
                return;

            if (replaceLineBreaks && !string.IsNullOrEmpty(text.text))
                text.text = text.text.Replace('\n', ' ');

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.localRotation = Quaternion.identity;
            text.rectTransform.localScale = Vector3.one;
        }
    }
}
