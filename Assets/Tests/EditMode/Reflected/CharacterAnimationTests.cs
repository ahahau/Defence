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

        /// <summary>
        /// 둘째 공격은 그림이 있을 때만 번갈아 쓰고, 맞는 동작은 휘두르는 중·쓰러진 뒤에는 끼어들지 않는다.
        /// 맞아도 휘두르기는 이어져 피해가 나가므로, 그림만 움찔하면 무기 없이 때리는 것처럼 보인다.
        /// </summary>
        [Test]
        public void CharacterAnimation_AlternatesAttacksAndHurtNeverCutsASwing()
        {
            Assert.AreEqual(CharacterClip.Attack, CharacterAnimationRules.NextAttack(false, true));
            Assert.AreEqual(CharacterClip.Attack, CharacterAnimationRules.NextAttack(true, false));
            Assert.AreEqual(CharacterClip.AttackAlt, CharacterAnimationRules.NextAttack(true, true));

            Assert.IsTrue(CharacterAnimationRules.AllowsHurt(CharacterClip.Idle));
            Assert.IsTrue(CharacterAnimationRules.AllowsHurt(CharacterClip.Walk));
            Assert.IsTrue(CharacterAnimationRules.AllowsHurt(CharacterClip.Hurt));
            Assert.IsFalse(CharacterAnimationRules.AllowsHurt(CharacterClip.Attack));
            Assert.IsFalse(CharacterAnimationRules.AllowsHurt(CharacterClip.AttackAlt));
            Assert.IsFalse(CharacterAnimationRules.AllowsHurt(CharacterClip.Defeated));

            Assert.IsTrue(CharacterAnimationRules.ReturnsToIdle(CharacterClip.AttackAlt));
            Assert.IsTrue(CharacterAnimationRules.ReturnsToIdle(CharacterClip.Hurt));
            Assert.IsFalse(CharacterAnimationRules.IsLooping(CharacterClip.Hurt));
        }

        /// <summary>
        /// 실제 재생기(<see cref="EntityRender"/>)에서: 평타마다 첫째·둘째 동작이 번갈아 나오고, 휘두르는 중의 피격은 동작을 끊지 않으며,
        /// 서 있을 때의 피격은 움찔 그림으로 바뀐다. 쓰러진 뒤의 피격·공격 요청은 쓰러진 그림을 덮지 않는다.
        /// </summary>
        [Test]
        public void EntityRender_AlternatesSwingsAndPlaysHurtOnlyWhenNotSwinging()
        {
            var texture = new Texture2D(4, 4);
            Sprite Frame(string name)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f));
                sprite.name = name;
                return sprite;
            }

            var set = ScriptableObject.CreateInstance<CharacterAnimationSetSO>();
            var setData = new SerializedObject(set);
            void Fill(string field, params Sprite[] sprites)
            {
                var property = setData.FindProperty(field);
                Assert.That(property, Is.Not.Null, $"CharacterAnimationSetSO.{field} 를 찾지 못했습니다.");
                property.arraySize = sprites.Length;
                for (var i = 0; i < sprites.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            Fill("idleFrames", Frame("idle"));
            Fill("attackFrames", Frame("attack1"), Frame("attack1b"));
            Fill("attackAltFrames", Frame("attack2"), Frame("attack2b"));
            Fill("defeatedFrames", Frame("dead"));
            Fill("hurtFrames", Frame("hit"));
            setData.ApplyModifiedPropertiesWithoutUndo();

            var go = new GameObject("RenderUnderTest");
            try
            {
                var spriteRenderer = go.AddComponent<SpriteRenderer>();
                var render = go.AddComponent<EntityRender>();
                var renderData = new SerializedObject(render);
                renderData.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
                renderData.FindProperty("animationSet").objectReferenceValue = set;
                renderData.ApplyModifiedPropertiesWithoutUndo();

                render.SetUnitSprite(EntityState.Attack);
                Assert.AreEqual("attack1", spriteRenderer.sprite.name);

                render.SetUnitSprite(EntityState.Hurt);
                Assert.AreEqual(CharacterClip.Attack, render.CurrentClip, "휘두르는 중의 피격이 공격 동작을 끊었다.");

                render.SetUnitSprite(EntityState.Attack);
                Assert.AreEqual("attack2", spriteRenderer.sprite.name, "둘째 평타가 둘째 동작으로 나오지 않았다.");

                render.SetUnitSprite(EntityState.Attack);
                Assert.AreEqual("attack1", spriteRenderer.sprite.name);

                // 공격이 끝나 대기로 돌아간 상태를 만든다(동작 끝은 Update가 처리하므로 쓰러졌다 일어나는 길로 돌린다).
                render.SetUnitSprite(EntityState.Defeated);
                render.SetUnitSprite(EntityState.Hurt);
                render.SetUnitSprite(EntityState.Attack);
                Assert.AreEqual("dead", spriteRenderer.sprite.name, "쓰러진 뒤 피격·공격이 쓰러진 그림을 덮었다.");

                render.SetUnitSprite(EntityState.Idle);
                render.SetUnitSprite(EntityState.Hurt);
                Assert.AreEqual("hit", spriteRenderer.sprite.name, "서 있을 때 피격이 움찔 그림으로 바뀌지 않았다.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(set);
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// 해골 팩(부하)은 맞는 동작 그림이 있으니 반드시 채운다. 원거리·지원 역할의 부하·모험가는 발사체 그림이 있어야 한다 —
        /// 예전에는 모두 흰 사각형이 날아갔다.
        /// </summary>
        [Test]
        public void CharacterAnimation_SkeletonsFlinchAndShootersHaveProjectileArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterAnimationSetSO", new[] { AnimationFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(path);
                var art = AssetDatabase.GetAssetPath(set.GetFrames(CharacterClip.Attack)[0]);
                if (art.Contains("/SkeletonWarrior/") || art.Contains("/SkeletonKing/"))
                    Assert.IsTrue(set.HasHurt, $"{path}: 해골 팩인데 맞는 동작(hit) 그림이 없다.");
            }

            var shooters = new System.Collections.Generic.List<(string owner, CharacterAnimationSetSO set)>();
            foreach (var guid in AssetDatabase.FindAssets("t:UnitDataSO"))
            {
                var unit = AssetDatabase.LoadAssetAtPath<Code.Units.UnitDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                var agent = unit.Prefab != null ? unit.Prefab.GetComponentInChildren<Code.BT.BattleAgent>(true) : null;
                var render = unit.Prefab != null ? unit.Prefab.GetComponentInChildren<EntityRender>(true) : null;
                if (agent != null && agent.UsesRangedKiting && render != null && render.AnimationSet != null)
                    shooters.Add((unit.name, render.AnimationSet));
            }

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<Code.Enemies.EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if ((enemy.Role == Code.BT.BattleRole.Ranged || enemy.Role == Code.BT.BattleRole.Support) && enemy.AnimationSet != null)
                    shooters.Add((enemy.name, enemy.AnimationSet));
            }

            Assert.IsNotEmpty(shooters, "원거리·지원 캐릭터를 하나도 찾지 못했다.");
            foreach (var (owner, set) in shooters)
            {
                Assert.IsNotEmpty(set.ProjectileFrames, $"{owner}({set.name}): 발사체 그림이 없어 사각형이 날아간다.");
                foreach (var frame in set.ProjectileFrames)
                    Assert.IsNotNull(frame, $"{owner}({set.name}): 발사체에 빈 프레임이 있다.");
            }
        }

        [Test]
        public void CharacterAnimation_EverySetHasAllClips()
        {
            var guids = AssetDatabase.FindAssets("t:CharacterAnimationSetSO", new[] { AnimationFolder });
            Assert.IsNotEmpty(guids, "애니메이션 묶음이 하나도 없다.");

            // 둘째 공격·맞는 동작은 팩에 그림이 있는 캐릭터만 있다. 네 기본 동작만 반드시 있어야 한다.
            var required = new[] { CharacterClip.Idle, CharacterClip.Walk, CharacterClip.Attack, CharacterClip.Defeated };
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(path);
                foreach (var clip in required)
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
