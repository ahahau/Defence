using _01.Code.Manager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace _01.Code.UI
{
    public class TownSceneMover : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private string sceneName;
        private WaveManager _waveManager;

        private void Awake()
        {
            _waveManager = GameManager.Instance?.GetManager<WaveManager>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_waveManager != null && _waveManager.IsRunning)
            {
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
