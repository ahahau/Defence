using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Portal : Building
    {
        [SerializeField]
        private Enemy enemyPrefab;

        [SerializeField]
        private float enemyTurnInterval = 5f;

        private Enemy spawnedEnemy;
        
        public void Initialize(Node installedNode)
        {
            if (spawnedEnemy != null || installedNode == null)
                return;

            spawnedEnemy = CreateEnemy(installedNode);
            spawnedEnemy.Initialize(installedNode, enemyTurnInterval);
        }

        private Enemy CreateEnemy(Node installedNode)
        {
            var spawnPosition = installedNode.EnemyPosition.position;
            if (enemyPrefab != null)
                return Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            var enemyObject = new GameObject("TestEnemy");
            enemyObject.transform.position = spawnPosition;
            var enemy = enemyObject.AddComponent<Enemy>();
            CreateEnemyFallbackVisual(enemyObject);
            return enemy;
        }

        private void CreateEnemyFallbackVisual(GameObject enemyObject)
        {
            var spriteRenderer = enemyObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateEnemySprite();
            spriteRenderer.color = Color.red;
            spriteRenderer.sortingOrder = 30;
            enemyObject.transform.localScale = Vector3.one * 0.5f;
        }

        private Sprite CreateEnemySprite()
        {
            const int size = 32;
            const float radius = 12f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = distance <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void OnDestroy()
        {
            if (spawnedEnemy != null)
                Destroy(spawnedEnemy.gameObject);
        }
    }
}
