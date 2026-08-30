using System;
using System.Collections.Generic;

namespace _01.Code.Persistence
{
    /// <summary>
    /// 저장 파일의 겉껍데기. 시스템별 조각을 열쇠와 함께 나란히 담는다.
    ///
    /// 조각의 속을 여기서 알지 못하는 것이 요점이다. 시스템 하나가 저장할 내용을
    /// 바꿔도 이 형식은 그대로고, 읽지 못하는 조각이 하나 있어도 나머지는 살아남는다.
    /// </summary>
    [Serializable]
    public sealed class RunSaveFile
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public string savedAtUtc;

        /// <summary>몇 일차를 끝낸 시점인가. 조각 하나의 것이 아니라 체크포인트 자체의 정보다.</summary>
        public int completedDay;

        public List<RunSaveEntry> entries = new();

        public string Find(string key)
        {
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].key == key)
                    return entries[i].json;

            return string.Empty;
        }
    }

    [Serializable]
    public struct RunSaveEntry
    {
        public string key;
        public string json;
    }
}
