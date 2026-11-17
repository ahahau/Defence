using System;
using System.IO;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>원자적 교체, 백업 보호, 종료 마커. 구 런/프로필에는 접근하지 않는다.</summary>
    public sealed class StageCheckpointStore
    {
        private const string FileName = "defence-stage-run-v1.json";
        private readonly string _path;
        private bool _canWrite = true;
        public string Error { get; private set; }

        public StageCheckpointStore(string folder) => _path = Path.Combine(folder, FileName);

        public static bool HasSave(string folder)
        {
            string path = Path.Combine(folder, FileName);
            return !File.Exists(path + ".ended") && (File.Exists(path) || File.Exists(path + ".bak"));
        }

        /// <summary>명시적 새 게임과 판 종료. 마커가 남는 동안 늦은 저장도 거절한다.</summary>
        public static bool EndRun(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, FileName) + ".ended", "ended");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"스테이지 종료 표시 실패: {exception.Message}");
                return false;
            }
        }

        internal bool Read(out StageCheckpointData data)
        {
            data = null;
            try
            {
                if (File.Exists(_path + ".ended"))
                {
                    // 새 판 시작은 기존 진행을 보관한 뒤에만 허용한다.
                    string archive = _path + ".archive-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N");
                    Directory.CreateDirectory(archive);
                    foreach (string suffix in new[] { "", ".bak", ".ended" })
                        if (File.Exists(_path + suffix))
                            File.Copy(_path + suffix, Path.Combine(archive, FileName + suffix));
                    File.Delete(_path);
                    File.Delete(_path + ".bak");
                    File.Delete(_path + ".ended");
                    return false;
                }
                if (!File.Exists(_path) && !File.Exists(_path + ".bak"))
                    return false;
                if (TryRead(_path, out data))
                    return true;
                bool restored = TryRead(_path + ".bak", out data);
                Block("스테이지 저장 원본을 읽지 못했습니다. 원본/백업 보호를 위해 자동 저장을 중단했습니다.");
                return restored;
            }
            catch (Exception exception)
            {
                Block($"스테이지 저장 접근 실패: {exception.Message}");
                return false;
            }
        }

        internal bool Write(StageCheckpointData data)
        {
            if (!_canWrite || File.Exists(_path + ".ended"))
                return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(_path))
                    File.Replace(temporary, _path, _path + ".bak");
                else
                    File.Move(temporary, _path);
                Error = null;
                return true;
            }
            catch (Exception exception)
            {
                Block($"스테이지 저장 실패: {exception.Message}");
                return false;
            }
        }

        internal void Block(string reason)
        {
            _canWrite = false;
            Error = reason;
            Debug.LogError(reason);
        }

        private static bool TryRead(string path, out StageCheckpointData data)
        {
            data = null;
            if (!File.Exists(path))
                return false;
            try
            {
                data = JsonUtility.FromJson<StageCheckpointData>(File.ReadAllText(path));
                return data != null && data.version == 1 && !string.IsNullOrWhiteSpace(data.stageId)
                    && data.loadout != null && data.placements != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
