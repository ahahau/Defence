using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>대화 상자 화면이 그릴 값. 선택지는 <see cref="DialogueView.Choices"/>에 따로 있다.</summary>
    public struct DialogueScreenState
    {
        public string Title;
        public string SpeakerName;
        public string Body;
        public string Progress;
        public bool NextVisible;
        public bool CloseVisible;

        /// <summary>뒤 화면 입력을 막는 덮개를 깔지.</summary>
        public bool BlocksBackground;

        public bool SpotlightVisible;

        /// <summary>강조할 화면 사각형(왼쪽 아래 원점 픽셀).</summary>
        public Rect SpotlightRect;

        public float SpotlightPadding;
    }
}
