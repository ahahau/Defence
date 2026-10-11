using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Code.Progression
{
    /// <summary>
    /// 메타 기록(<see cref="MetaProfile"/>)을 디스크에 두고 읽고 쓴다.
    ///
    /// 판 저장과 다른 파일이다 — 판 저장은 판이 끝나면 지우지만 이 기록은 남아야 한다.
    /// 쓸 때는 임시 파일에 쓴 뒤 바꿔치기해서, 쓰는 도중 꺼져도 이전 기록이 깨지지 않게 한다.
    /// </summary>
    public static class MetaProfileStore
    {
        public const string FileName = "defence-meta.json";

        /// <summary>테스트가 기록 위치를 바꿀 때 쓴다. 비우면 기본 위치.</summary>
        public static string PathOverride { get; set; }

        private static MetaProfile _cached;

        public static string SavePath =>
            string.IsNullOrEmpty(PathOverride) ? Path.Combine(Application.persistentDataPath, FileName) : PathOverride;

        public static MetaProfile Profile => _cached ??= Load();

        /// <summary>가장 최근에 끝난 판의 메타 결과. 결과 화면이 읽는다.</summary>
        public static MetaRunResult? LastResult { get; private set; }

        /// <summary>디스크에서 다시 읽는다. 테스트와 경로를 바꾼 뒤에 쓴다.</summary>
        public static void Reload()
        {
            _cached = null;
            LastResult = null;
        }

        /// <summary>
        /// 새 판을 시작한다. 장착한 보정 중 열려 있고 칸에 들어가는 것만 이번 판 보정으로 확정한다.
        /// </summary>
        /// <returns>이번 판에 건 보정.</returns>
        public static IReadOnlyList<int> BeginNewRun()
        {
            var profile = Profile;
            var run = new MetaRunState { active = true };
            var slots = MetaProgressRules.SlotCount(profile.progress);
            foreach (var value in profile.equipped)
            {
                if (run.modifiers.Count >= slots)
                    break;
                if (IsValid(value) && MetaProgressRules.IsUnlocked((MetaModifier)value, profile.progress)
                    && !run.modifiers.Contains(value))
                    run.modifiers.Add(value);
            }

            profile.currentRun = run;
            Save();
            return run.modifiers;
        }

        /// <summary>하루 영업이 끝났다. 이번 판의 처치와 최고 등급을 쌓는다.</summary>
        public static void RecordWaveEnd(int kills, int grade)
        {
            var run = Profile.currentRun;
            run.active = true;
            run.kills += Mathf.Max(0, kills);
            run.bestGrade = Mathf.Max(run.bestGrade, grade);
            Save();
        }

        public static void RecordBossDefeat()
        {
            var run = Profile.currentRun;
            run.active = true;
            run.bossesDefeated++;
            Save();
        }

        /// <summary>
        /// 판이 끝났다. 진행도를 더하고 최고 기록을 고친 뒤 이번 판 기록을 비운다.
        /// </summary>
        public static MetaRunResult RecordRunEnd(int daysSurvived, int finalGrade)
        {
            var profile = Profile;
            var run = profile.currentRun ?? new MetaRunState();
            var days = Mathf.Max(0, daysSurvived);
            var points = MetaProgressRules.RunPoints(days, run.bossesDefeated);
            var slotsBefore = MetaProgressRules.SlotCount(profile.progress);
            var newBestDay = days > profile.bestDay;

            profile.runsPlayed++;
            profile.progress += points;
            profile.bestDay = Mathf.Max(profile.bestDay, days);
            profile.bestGrade = Mathf.Max(profile.bestGrade, Mathf.Max(run.bestGrade, finalGrade));
            profile.bestKills = Mathf.Max(profile.bestKills, run.kills);
            profile.totalKills += run.kills;
            profile.totalBossesDefeated += run.bossesDefeated;
            profile.currentRun = new MetaRunState();
            Save();

            var result = new MetaRunResult(
                points, profile.progress, days, newBestDay, slotsBefore, MetaProgressRules.SlotCount(profile.progress));
            LastResult = result;
            return result;
        }

        /// <summary>
        /// 다음 판에 낄 보정을 켜고 끈다. 잠긴 보정이나 칸이 꽉 찼을 때는 켜지 않는다.
        /// </summary>
        /// <returns>바뀌었으면 true.</returns>
        public static bool ToggleEquip(MetaModifier modifier)
        {
            var profile = Profile;
            var value = (int)modifier;
            if (profile.equipped.Remove(value))
            {
                Save();
                return true;
            }

            if (!MetaProgressRules.IsUnlocked(modifier, profile.progress)
                || profile.equipped.Count >= MetaProgressRules.SlotCount(profile.progress))
                return false;

            profile.equipped.Add(value);
            Save();
            return true;
        }

        public static bool IsEquipped(MetaModifier modifier) => Profile.equipped.Contains((int)modifier);

        public static void Save()
        {
            if (_cached == null)
                return;

            try
            {
                var path = SavePath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(_cached, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"메타 기록을 저장하지 못했습니다: {exception.Message}");
            }
        }

        private static MetaProfile Load()
        {
            try
            {
                var path = SavePath;
                if (File.Exists(path))
                {
                    var profile = JsonUtility.FromJson<MetaProfile>(File.ReadAllText(path));
                    if (profile != null)
                    {
                        profile.equipped ??= new List<int>();
                        profile.currentRun ??= new MetaRunState();
                        profile.currentRun.modifiers ??= new List<int>();
                        return profile;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"메타 기록을 읽지 못해 새로 시작합니다: {exception.Message}");
            }

            return new MetaProfile();
        }

        private static bool IsValid(int value) => Enum.IsDefined(typeof(MetaModifier), value);
    }
}
