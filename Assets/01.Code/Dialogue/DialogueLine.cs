using System;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [Serializable]
    public struct DialogueLine
    {
        [SerializeField] private string speakerName;
        [SerializeField, TextArea(2, 6)] private string text;

        public string SpeakerName => speakerName;
        public string Text => text;
    }
}
