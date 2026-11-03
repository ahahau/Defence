using System.Text;
using Code.Enemies;
using Code.Stages;

namespace Code.UI.Stages
{
    /// <summary>실제 스테이지/웨이브에 있는 입구·규칙·장치만 안내한다.</summary>
    internal sealed class StageBriefingPresenter
    {
        private readonly StringBuilder _text = new();
        private StageDefinitionSO _definition;
        private StageCatalogSO _catalog;
        private int _waveIndex = -1;
        public string Doors { get; private set; } = string.Empty;
        public string NextDoors { get; private set; } = string.Empty;
        public string Rules { get; private set; } = string.Empty;
        public string Devices { get; private set; } = string.Empty;
        public string Enemies { get; private set; } = string.Empty;

        internal void Refresh(StageDefinitionSO definition, int waveIndex, StageCatalogSO catalog)
        {
            if (_definition == definition && _waveIndex == waveIndex && _catalog == catalog)
                return;
            _definition = definition;
            _catalog = catalog;
            _waveIndex = waveIndex;
            int open = definition.OpenDoorCount(waveIndex);
            int added = waveIndex > 0 ? open - definition.OpenDoorCount(waveIndex - 1) : 0;
            Doors = $"입구 {open}/{definition.Doors.Length}" + (added > 0 ? $" (이번 +{added})" : string.Empty);
            NextDoors = string.Empty;
            for (int next = waveIndex + 1; next < definition.Waves.Length; next++)
            {
                int extra = definition.OpenDoorCount(next) - open;
                if (extra <= 0)
                    continue;
                NextDoors = $" · {next + 1}웨이브에 +{extra}";
                break;
            }
            Rules = RulesFor(definition.Waves[waveIndex]);
            Devices = DevicesFor(definition);
            Enemies = EnemiesFor(definition.Waves[waveIndex], catalog);
        }

        private string EnemiesFor(StageWave wave, StageCatalogSO catalog)
        {
            _text.Clear();
            int total = 0;
            int regular = wave.Enemies?.Length ?? 0;
            for (int i = 0; i < wave.Count; i++)
            {
                EnemyDataSO data = wave.At(i, out bool isBoss);
                if (data == null)
                    continue;
                total++;
                StageEnemyRule rule = wave.RuleAt(i);
                int count = 0;
                bool alreadyListed = false;
                // 같은 종류도 역할과 특수 규칙을 나누고, 최초 등장 위치에서만 인원을 표시한다.
                for (int j = 0; j < wave.Count; j++)
                {
                    if (wave.At(j, out bool otherBoss) != data || otherBoss != isBoss
                        || (i < regular) != (j < regular) || wave.RuleAt(j) != rule)
                        continue;
                    if (j < i)
                    {
                        alreadyListed = true;
                        break;
                    }
                    count++;
                }
                if (alreadyListed)
                    continue;
                string name = string.IsNullOrWhiteSpace(data.Name) ? "이름 미지정" : data.Name;
                string role = isBoss ? "보스 " : i >= regular ? "호위 " : string.Empty;
                if (!isBoss && catalog != null && catalog.IsElite(data)
                    && !name.StartsWith("정예", System.StringComparison.Ordinal))
                    role += "정예 ";
                string mark = rule == StageEnemyRule.Vault ? "(도약)"
                    : rule == StageEnemyRule.Rush ? "(돌진)" : string.Empty;
                Append($"{role}{name}{mark} {count}");
            }
            return $"침입 예정 {total}명" + (_text.Length > 0 ? " · " + _text.ToString() : string.Empty);
        }

        private string RulesFor(StageWave wave)
        {
            bool vault = false;
            bool rush = false;
            // 보스/호위와 스폰되지 않는 여분 규칙은 안내에 포함하지 않는다.
            for (int i = 0; i < (wave.Enemies?.Length ?? 0); i++)
            {
                if (wave.Enemies[i] == null)
                    continue;
                StageEnemyRule rule = wave.RuleAt(i);
                vault |= rule == StageEnemyRule.Vault;
                rush |= rule == StageEnemyRule.Rush;
            }
            _text.Clear();
            if (vault)
                Append("주황 도약(바리케이드 넘음)");
            if (rush)
                Append("빨강 돌진(교전 무시)");
            return _text.Length > 0 ? "표식 적: " + _text.ToString() : string.Empty;
        }

        private string DevicesFor(StageDefinitionSO definition)
        {
            bool fire = false;
            bool gate = false;
            bool rockfall = false;
            bool push = false;
            bool hazard = false;
            bool pit = false;
            if (definition.Devices != null)
                foreach (StageDevice device in definition.Devices)
                {
                    if (device == null)
                        continue;
                    fire |= device.Kind == StageDeviceKind.FireVent;
                    gate |= device.Kind == StageDeviceKind.Gate;
                    rockfall |= device.Kind == StageDeviceKind.Rockfall;
                    push |= device.Kind == StageDeviceKind.Push;
                    hazard |= device.Kind == StageDeviceKind.Hazard;
                    pit |= device.Kind == StageDeviceKind.Pit;
                }
            _text.Clear();
            if (fire)
                Append("주황 불(아군도 피해)");
            if (gate)
                Append("회색 문(주기 차단)");
            if (rockfall)
                Append("낙석(금색일 때 누름)");
            if (push)
                Append("금색 화살표(클릭 밀치기)");
            if (hazard)
                Append("주황 바닥(지속 피해)");
            if (pit)
                Append("검은 구덩이(밀면 낙하)");
            return _text.Length > 0 ? "장치: " + _text.ToString() : string.Empty;
        }

        private void Append(string value)
        {
            if (_text.Length > 0)
                _text.Append(" · ");
            _text.Append(value);
        }
    }
}
