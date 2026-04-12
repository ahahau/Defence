using System.Collections.Generic;
using UnityEngine;

namespace GondrLib.ObjectPool.Runtime
{
    [CreateAssetMenu(fileName = "PoolManager", menuName = "SO/Pool/Manager", order = 0)]
    public class PoolManagerSO : ScriptableObject
    {
        public List<PoolingItemSO> itemList = new List<PoolingItemSO>();
        
        //해당 아이템이 존재하는 풀을 만든다.
        private Dictionary<PoolingItemSO, Pool> _pools;
        private Transform _rootTrm;

        public void Initialize(Transform rootTrm)
        {
            _rootTrm = rootTrm;
            _pools = new Dictionary<PoolingItemSO, Pool>();

            foreach (var item in itemList)
            {
                if (item == null)
                {
                    Debug.LogWarning("PoolManagerSO has a null PoolingItemSO entry.", this);
                    continue;
                }

                if (item.prefab == null)
                {
                    Debug.LogWarning($"Pooling item `{item.name}` has no prefab assigned.", item);
                    continue;
                }

                IPoolable poolable = item.prefab.GetComponent<IPoolable>();
                if (poolable == default(IPoolable))
                {
                    Debug.LogWarning($"Pooling item `{item.prefab.name}` has no IPoolable component.", item.prefab);
                    continue;
                }

                if (poolable.PoolingType == null)
                {
                    Debug.LogWarning($"Pooling prefab `{item.prefab.name}` has no PoolingType assigned.", item.prefab);
                    continue;
                }

                GameObject newParent = new GameObject(poolable.PoolingType.poolingName);
                newParent.transform.SetParent(_rootTrm);
                
                Pool pool = new Pool(poolable, newParent.transform, item.initCount);
                _pools.Add(item, pool); //딕셔너리에 추가한다.
            }
        }

        public IPoolable Pop(PoolingItemSO findItem)
        {
            if (_pools.TryGetValue(findItem, out Pool pool))
            {
                return pool.Pop();
            }

            return default;
        }

        public void Push(IPoolable item)
        {
            if (_pools.TryGetValue(item.PoolingType, out Pool pool))
            {
                pool.Push(item);
            }
        }
    }
}
