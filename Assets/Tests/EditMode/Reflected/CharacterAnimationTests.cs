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
