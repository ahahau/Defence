using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Test
{
    public class TempController : MonoBehaviour, ISaveable
    {
        private float _xMove;
        private Rigidbody2D _rigidbody;
        
        [field:SerializeField]public int Exp {get; private set;}
        [field:SerializeField]public int Gold {get; private set;}
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            ReadXAxisInput();
            _rigidbody.linearVelocityX = _xMove * 5f;
        }

        private void ReadXAxisInput()
        {
            _xMove = 0;
            if (Keyboard.current.aKey.isPressed)
            {
                _xMove -= 1;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                _xMove += 1;
            }
        }

        #region Save load logic
        public struct  PlayerSaveData
        {
            public int gold;
            public int exp;
        }
            [field:SerializeField]public SaveID SaveId { get; private set; }
            public string GetSaveData()
            {
                PlayerSaveData saveData = new PlayerSaveData
                {
                    gold = Gold,
                    exp = Exp
                };
                return JsonUtility.ToJson(saveData);
            }

            public void RestoreData(string savedData)
            {
                PlayerSaveData loadedData = JsonUtility.FromJson<PlayerSaveData>(savedData);
                Gold = loadedData.gold;
                Exp = loadedData.exp;
            }
        #endregion
    }
}