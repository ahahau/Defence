using UnityEngine;

namespace Code.Test
{
    [CreateAssetMenu(fileName = "Save id", menuName = "SO/SaveId", order = 0)]
    public class SaveID : ScriptableObject
    {
        public int saveID;
        public string saveName;
    }
}