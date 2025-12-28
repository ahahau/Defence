using UnityEngine;

namespace _01.Code.Enemies
{
    [CreateAssetMenu(fileName = "Animator param", menuName = "SO/Animator/Param", order = 10)]
    public class ParamSO : ScriptableObject
    {
        [field: SerializeField] public string ParamName { get; private set; }
        [field: SerializeField] public int HashValue { get; private set; }

        private void OnValidate()
        {
            HashValue = Animator.StringToHash(ParamName);
        }
    }
}