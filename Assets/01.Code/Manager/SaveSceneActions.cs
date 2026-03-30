using UnityEngine;

namespace _01.Code.Manager
{
    public class SaveSceneActions : MonoBehaviour
    {
        public void SaveGame()
        {
            GameManager.Instance?.SaveManager?.SaveGame();
        }

        public void LoadGame()
        {
            GameManager.Instance?.SaveManager?.LoadGame();
        }

        public void DeleteSave()
        {
            GameManager.Instance?.SaveManager?.DeleteSave();
        }

        public void ReloadCurrentScene()
        {
            GameManager.Instance?.SaveManager?.ReloadCurrentScene();
        }

        public void ReloadCurrentSceneFromSave()
        {
            GameManager.Instance?.SaveManager?.ReloadCurrentSceneFromSave();
        }
    }
}
