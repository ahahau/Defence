using UnityEngine;

namespace _01.Code.UI
{
    public abstract class UIBaseView : MonoBehaviour
    {
        public bool IsVisible => gameObject.activeSelf;

        public virtual void Initialize()
        {
            gameObject.SetActive(false);
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
