using UnityEngine;

namespace Code.UI.Toolkit
{
    /// <summary>상인 진열 카드 한 장에 그릴 값.</summary>
    public struct MerchantOfferViewModel
    {
        public string Name;
        public int Price;
        public bool Affordable;
        public string Body;
        public Sprite Icon;
        public bool IsRandom;

        /// <summary>이 유물이 맞물리는 조합 안내. 없으면 빈 문자열.</summary>
        public string Combo;

        /// <summary>짝을 이미 가져 조합이 완성되는가. 조합 줄 색이 바뀐다.</summary>
        public bool ComboComplete;
    }

    /// <summary>상인 창 아래 결과 줄의 분위기.</summary>
    public enum MerchantDetailTone
    {
        Normal,
        Success,
        Failure
    }
}
