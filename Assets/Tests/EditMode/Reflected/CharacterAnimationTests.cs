using Code.Entities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 캐릭터 프레임 애니메이션. 대기·걷기는 돌고, 공격·쓰러짐은 마지막 프레임에 멈춰야 한다.
    /// 쓰러짐이 돌면 죽은 모험가가 다시 일어서는 그림이 되고, 공격이 멈추지 않으면 끝을 알 수 없다.
    /// </summary>
    public class CharacterAnimationTests
    {
        private const string AnimationFolder = "Assets/07.SO/Characters/Animations";

        [Test]
        public void CharacterAnimation_LoopingClipWraps()
        {
            Assert.AreEqual(0, CharacterAnimationRules.GetFrameIndex(6, 0f, 10f, true));
            Assert.AreEqual(5, CharacterAnimationRules.GetFrameIndex(6, 0.55f, 10f, true));
            Assert.AreEqual(1, CharacterAnimationRules.GetFrameIndex(6, 0.75f, 10f, true));
        }

        [Test]
        public void CharacterAnimation_OneShotClipHoldsLastFrame()
        {
            Assert.AreEqual(5, CharacterAnimationRules.GetFrameIndex(6, 3f, 10f, false));
            Assert.IsFalse(CharacterAnimationRules.IsFinished(6, 0.59f, 10f));
            Assert.IsTrue(CharacterAnimationRules.IsFinished(6, 0.6f, 10f));
        }

        [Test]
        public void CharacterAnimation_OnlyIdleAndWalkLoop()
        {
            Assert.IsTrue(CharacterAnimationRules.IsLooping(CharacterClip.Idle));
            Assert.IsTrue(CharacterAnimationRules.IsLooping(CharacterClip.Walk));
            Assert.IsFalse(CharacterAnimationRules.IsLooping(CharacterClip.Attack));
            Assert.IsFalse(CharacterAnimationRules.IsLooping(CharacterClip.Defeated));
        }

        /// <summary>
        /// 좌우는 몸통 회전 Y(0/180) 하나로 정한다. 피격 흔들림이 회전을 자기 시작값으로 되돌려 Y를 덮으면
        /// 방향을 다시 얹는데, 그때 z 기울기(흔들림)는 살아 있어야 한다. 예전에는 flipX와 크기 x 뒤집기가 겹쳐
        /// 두 번 뒤집힌 캐릭터가 한쪽만 봤다.
        /// </summary>
        [TestCase(0f, 0f)]
        [TestCase(0f, 12f)]
        [TestCase(180f, 12f)]
        [TestCase(180f, -20f)]
        public void CharacterFacing_KeepsTiltWhenTurned(float facingY, float tiltZ)
        {
            var rotation = Quaternion.Euler(0f, facingY, tiltZ);
            var tilt = CharacterFacingRules.TiltZ(rotation * Vector3.up, rotation * Vector3.forward);
            Assert.That(tilt, Is.EqualTo(tiltZ).Within(0.01f));
        }

        /// <summary>
        /// 무기가 닿는 때는 공격 동작의 중간 무렵이다. 예비 동작은 공격 간격의 절반을 넘지 않는다 —
        /// 넘으면 게이지가 차기 한참 전부터 휘둘러 다음 공격과 겹친다.
        /// </summary>
        [Test]
        public void CharacterAnimation_ImpactLandsMidSwingWithinHalfTheInterval()
        {
            Assert.That(CharacterAnimationRules.ImpactSeconds(6, 12f, 0.55f), Is.EqualTo(3f / 12f).Within(0.0001f));
            Assert.That(CharacterAnimationRules.ImpactSeconds(0, 12f, 0.55f), Is.Zero);
            Assert.That(CharacterAnimationRules.WindupSeconds(0.25f, 1.2f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(CharacterAnimationRules.WindupSeconds(0.25f, 0.4f), Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void CharacterAnimation_EverySetHasAllClips()
        {
            var guids = AssetDatabase.FindAssets("t:CharacterAnimationSetSO", new[] { AnimationFolder });
            Assert.IsNotEmpty(guids, "애니메이션 묶음이 하나도 없다.");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(path);
                foreach (CharacterClip clip in System.Enum.GetValues(typeof(CharacterClip)))
                {
                    var frames = set.GetFrames(clip);
                    Assert.IsNotEmpty(frames, $"{path}: {clip} 프레임이 없다.");
                    foreach (var frame in frames)
                        Assert.IsNotNull(frame, $"{path}: {clip}에 빈 프레임이 있다.");
                }
            }
        }
    }
}
