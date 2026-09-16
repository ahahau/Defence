using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.EditorTools
{
    /// <summary>
    /// 아트를 걷어낸 자리에 도트풍 임시 그림을 채운다.
    ///
    /// 그림을 지우면 참조가 끊겨 화면이 흰 사각형과 보라색 머티리얼이 된다. 그 상태로는
    /// 무엇이 어디 있는지 보이지 않아 게임을 돌려 볼 수도, 밸런스를 볼 수도 없다.
    /// 진짜 도트 아트가 들어오기 전까지 자리를 지키는 것이 이 그림들의 일이다.
    ///
    /// 한 장짜리 회색 사각형으로 때우지 않는 이유는, 그러면 모험가와 함정과 버튼이 전부
    /// 같아 보여서 화면을 읽을 수 없기 때문이다. 종류마다 색을 다르게 주고 이름표를
    /// 남겨 둔다 — 못생겼지만 무엇인지는 읽힌다.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        private const string Root = "Assets/02.Art/Placeholder";

        /// <summary>도트 느낌을 내려면 작아야 한다. 크게 잡으면 그냥 흐릿한 사각형이 된다.</summary>
        private const int SpriteSize = 32;

        /// <summary>종류마다 다른 색. 화면에서 무엇이 무엇인지 구분되는 최소 조건이다.</summary>
        private static readonly (string name, Color fill)[] Kinds =
        {
            ("Adventurer", new Color32(0xC8, 0x5A, 0x4A, 0xFF)),
            ("Unit", new Color32(0x4A, 0x7A, 0xC8, 0xFF)),
            ("Boss", new Color32(0x8A, 0x3A, 0x9A, 0xFF)),
            ("Building", new Color32(0x7A, 0x6A, 0x4A, 0xFF)),
            ("Trap", new Color32(0xA8, 0x8A, 0x2A, 0xFF)),
            ("Node", new Color32(0x3A, 0x3A, 0x42, 0xFF)),
            ("Icon", new Color32(0x6A, 0x6A, 0x72, 0xFF)),
            ("Ui", new Color32(0x2A, 0x2C, 0x32, 0xFF)),
            ("Background", new Color32(0x18, 0x1A, 0x1E, 0xFF))
        };

        [MenuItem("Defence/Art/Generate Placeholder Art")]
        public static void Generate()
        {
            Directory.CreateDirectory(Root);

            foreach (var (name, fill) in Kinds)
                WriteSprite(name, fill);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[Placeholder] {Kinds.Length}종 생성: {Root}");
        }

        /// <summary>
        /// 한 장을 그려 png로 남긴다.
        ///
        /// 가장자리를 어둡게 두르고 안쪽을 한 단계 밝게 해서, 배경 위에 겹쳐도 덩어리의
        /// 경계가 보이게 한다. 테두리가 없으면 어두운 방에서 어두운 말이 사라진다.
        /// </summary>
        private static void WriteSprite(string name, Color fill)
        {
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            var edge = Color.Lerp(fill, Color.black, 0.45f);
            var light = Color.Lerp(fill, Color.white, 0.18f);

            for (var y = 0; y < SpriteSize; y++)
            for (var x = 0; x < SpriteSize; x++)
            {
                var onEdge = x < 2 || y < 2 || x >= SpriteSize - 2 || y >= SpriteSize - 2;
                // 위쪽 절반만 밝게 해서 평평한 색면이 아니라 입체로 읽히게 한다.
                var color = onEdge ? edge : y > SpriteSize / 2 ? light : fill;
                texture.SetPixel(x, y, color);
            }

            texture.Apply();
            var path = $"{Root}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            // 도트는 뭉개지면 안 된다. 필터를 끄고 압축도 하지 않는다.
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = SpriteSize;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{name}.png");

        /// <summary>
        /// 끊긴 그림 참조를 임시 그림으로 메운다. 비어 있던 자리는 건드리지 않는다 —
        /// 원래 비워 두기로 한 칸까지 채우면 무엇이 진짜 빈 칸인지 알 수 없게 된다.
        /// </summary>
        [MenuItem("Defence/Art/Fill Missing Sprite References")]
        public static void FillMissingReferences()
        {
            if (Load("Adventurer") == null)
                Generate();

            var filled = 0;
            filled += FillScriptableObjects();
            filled += FillPrefabs();

            AssetDatabase.SaveAssets();
            Debug.Log($"[Placeholder] 끊긴 참조 {filled}곳을 메웠습니다.");
        }

        /// <summary>
        /// 사라진 스크립트를 가리키는 죽은 컴포넌트를 프리팹에서 떼어낸다.
        ///
        /// 에셋 팩을 지우면 그 팩의 컴포넌트를 달고 있던 프리팹이 "손상되었을 수 있음"으로
        /// 임포트에 실패한다. 실패한 프리팹은 저장도 안 되므로, 임시 그림을 채워 넣어도
        /// 그 자리만 계속 비어 있다. 그림을 메우기 전에 먼저 치워야 한다.
        /// </summary>
        [MenuItem("Defence/Art/Strip Missing Script Components")]
        public static void StripMissingScripts()
        {
            var stripped = 0;
            var touched = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/04.Prefab" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = PrefabUtility.LoadPrefabContents(path);
                if (contents == null)
                    continue;

                var removed = 0;
                foreach (var child in contents.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    stripped += removed;
                    touched++;
                }

                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Placeholder] 프리팹 {touched}개에서 죽은 컴포넌트 {stripped}개를 떼어냈습니다.");
        }

        private static int FillScriptableObjects()
        {
            var filled = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/03.SO") && !path.StartsWith("Assets/Resources"))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                    continue;

                var sprite = Load(ResolveKindFor(path, asset.name));
                if (sprite == null)
                    continue;

                var serialized = new SerializedObject(asset);
                if (!FillSpriteFields(serialized, sprite, ref filled))
                    continue;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }

            return filled;
        }

        private static int FillPrefabs()
        {
            var filled = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/04.Prefab" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                var kind = ResolveKindFor(path, root.name);
                var sprite = Load(kind);
                if (sprite == null)
                    continue;

                var changed = false;

                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite != null)
                        continue;
                    renderer.sprite = sprite;
                    filled++;
                    changed = true;
                }

                // UI는 스프라이트가 비면 흰 사각형으로 나온다. 흰 사각형은 "빈 것"과
                // 구분되지 않아, 배치가 틀어졌는지 그림이 없는 건지 알 수 없다.
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.sprite != null)
                        continue;
                    image.sprite = Load("Ui");
                    filled++;
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(root);
            }

            return filled;
        }

        /// <summary>
        /// 경로와 이름으로 어느 색을 줄지 고른다. 정확할 필요는 없고, 화면에서
        /// 모험가와 함정이 구분되기만 하면 된다.
        /// </summary>
        private static string ResolveKindFor(string path, string assetName)
        {
            var haystack = (path + "/" + assetName).ToLowerInvariant();

            if (haystack.Contains("boss")) return "Boss";
            if (haystack.Contains("trap")) return "Trap";
            if (haystack.Contains("enem") || haystack.Contains("adventurer")) return "Adventurer";
            if (haystack.Contains("unit")) return "Unit";
            if (haystack.Contains("building") || haystack.Contains("buildings")) return "Building";
            if (haystack.Contains("node")) return "Node";
            if (haystack.Contains("ui/") || haystack.Contains("hud")) return "Ui";
            if (haystack.Contains("artifact") || haystack.Contains("stat")) return "Icon";
            return "Icon";
        }

        private static bool FillSpriteFields(SerializedObject serialized, Sprite sprite, ref int filled)
        {
            var changed = false;
            var iterator = serialized.GetIterator();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (iterator.objectReferenceValue != null)
                    continue;
                // 필드가 무슨 타입인지는 이름만으로는 모른다. 스프라이트 자리인지
                // 확인하지 않고 넣으면 프리팹 슬롯에 그림을 쑤셔 넣게 된다.
                if (!IsSpriteField(serialized.targetObject, iterator.propertyPath))
                    continue;

                iterator.objectReferenceValue = sprite;
                filled++;
                changed = true;
            }

            return changed;
        }

        private static readonly Dictionary<string, bool> SpriteFieldCache = new();

        private static bool IsSpriteField(UnityEngine.Object target, string propertyPath)
        {
            var key = target.GetType().FullName + "|" + propertyPath;
            if (SpriteFieldCache.TryGetValue(key, out var cached))
                return cached;

            var isSprite = ResolveFieldType(target.GetType(), propertyPath) == typeof(Sprite);
            SpriteFieldCache[key] = isSprite;
            return isSprite;
        }

        private static Type ResolveFieldType(Type type, string propertyPath)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;

            foreach (var segment in propertyPath.Split('.'))
            {
                if (type == null)
                    return null;

                System.Reflection.FieldInfo field = null;
                for (var walk = type; walk != null && field == null; walk = walk.BaseType)
                    field = walk.GetField(segment, flags);

                if (field == null)
                    return null;

                type = field.FieldType;
            }

            return type;
        }
    }
}
