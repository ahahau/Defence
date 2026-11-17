using Code.Manager;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 엘리트·보스 일시정지 잠금.
    ///
    /// 잠금이 새면 강적 앞에서 멈춰 놓고 배치를 다시 짜는 것이 정답이 되고,
    /// 풀리지 않으면 웨이브가 끝난 뒤에도 일시정지가 막혀 관리를 못 한다.
    /// </summary>
    public class PauseLockTests
    {
        private GameObject _host;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale;
            _host = new GameObject("Speed");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Time.timeScale = _timeScale;
        }

        [TestCase(true, false, 1, false, TestName = "Rules_OrdinaryEnemyDoesNotLock")]
        [TestCase(true, false, 3, false, TestName = "Rules_TierThreeIsNotYetElite")]
        [TestCase(true, false, 4, true, TestName = "Rules_TierFourIsElite")]
        [TestCase(true, true, 1, true, TestName = "Rules_BossAlwaysLocks")]
        [TestCase(false, true, 6, false, TestName = "Rules_FallenBossNoLongerLocks")]
        public void LocksPause(bool isAlive, bool isBoss, int tier, bool expected)
        {
            Assert.That(PauseLockRules.LocksPause(isAlive, isBoss, tier), Is.EqualTo(expected));
        }

        [Test]
        public void Lock_ResumesAPlayerPauseAtTheLastRunningSpeed()
        {
            var speed = _host.AddComponent<GameSpeedController>();
            speed.SetSetting(GameSpeedController.FastSpeed);
            speed.SetSetting(GameSpeedController.PausedSpeed);

            speed.SetPauseLock(_host, true, PauseLockRules.BossLockReason);

            Assert.That(speed.Setting, Is.EqualTo(GameSpeedController.FastSpeed));
            Assert.That(speed.PauseLockReason, Is.EqualTo(PauseLockRules.BossLockReason));
        }

        [Test]
        public void Lock_RefusesPauseButAllowsSpeedChanges()
        {
            var speed = _host.AddComponent<GameSpeedController>();
            speed.SetPauseLock(_host, true, PauseLockRules.EliteLockReason);

            speed.SetSetting(GameSpeedController.PausedSpeed);
            Assert.That(speed.IsPausedByPlayer, Is.False);

            speed.SetSetting(GameSpeedController.FastSpeed);
            Assert.That(speed.Setting, Is.EqualTo(GameSpeedController.FastSpeed));
        }

        [Test]
        public void Unlock_LetsThePlayerPauseAgain()
        {
            var speed = _host.AddComponent<GameSpeedController>();
            speed.SetPauseLock(_host, true, PauseLockRules.EliteLockReason);
            speed.SetPauseLock(_host, false, null);

            speed.SetSetting(GameSpeedController.PausedSpeed);

            Assert.That(speed.IsPauseLocked, Is.False);
            Assert.That(speed.PauseLockReason, Is.Empty);
            Assert.That(speed.IsPausedByPlayer, Is.True);
        }

        [Test]
        public void Lock_DoesNotBlockModalSuspensions()
        {
            var speed = _host.AddComponent<GameSpeedController>();
            speed.SetPauseLock(_host, true, PauseLockRules.BossLockReason);

            // 강제 선택 창은 잠금과 무관하게 시간을 세운다.
            speed.Suspend(_host);

            Assert.That(speed.IsPaused, Is.True);
            Assert.That(speed.IsPausedByPlayer, Is.False);
        }
    }
}
