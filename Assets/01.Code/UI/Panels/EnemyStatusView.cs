using Code.Enemies;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 모험가 상태 창(uGUI, A안). 모험가나 기록판 파티 줄을 누르면 그 자리 옆에 뜨고, 0.12초마다 다시 읽는다.
    /// 요청은 <see cref="EnemyStatusPanelView"/>가 넘기고, 문구는 <see cref="EnemyStatusPresenter"/>가 만든다.
    /// 상태이상이 없어도 자리를 지킨다.
    /// </summary>
    public sealed class EnemyStatusView : MonoBehaviour, IEscapeClosable
    {
        private const float RefreshInterval = 0.12f;
        private static readonly Vector2 ScreenOffset = new(24f, 24f);

        [SerializeField] private EnemyStatusPanelView source;
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text state;
        [SerializeField] private TMP_Text hp;
        [SerializeField] private TMP_Text attack;
        [SerializeField] private TMP_Text purpose;
        [SerializeField] private TMP_Text party;
        [SerializeField] private TMP_Text mood;
        [SerializeField] private TMP_Text satisfaction;
        [SerializeField] private TMP_Text effectsTitle;
        [SerializeField] private TMP_Text[] effects;
        [SerializeField] private UiFrameButton closeButton;

        private readonly EnemyStatusPresenter _presenter = new();
        private Enemy _target;
        private float _refreshTimer;

        private void OnEnable()
        {
            closeButton.Button.onClick.AddListener(Hide);
            if (source != null)
            {
                source.ShowRequested += Show;
                source.HideRequested += Hide;
            }

            root.SetActive(false);
        }

        private void OnDisable()
        {
            closeButton.Button.onClick.RemoveListener(Hide);
            if (source != null)
            {
                source.ShowRequested -= Show;
                source.HideRequested -= Hide;
            }
        }

        private void Update()
        {
            if (_target == null)
                return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = RefreshInterval;
            Refresh();
        }

        private void Show(Enemy enemy, Vector2 screenPosition)
        {
            _target = enemy;
            _refreshTimer = RefreshInterval;
            root.SetActive(true);
            Refresh();
            UiPlacement.PlaceNear(card, screenPosition, ScreenOffset);
        }

        private void Hide()
        {
            _target = null;
            root.SetActive(false);
        }

        bool IEscapeClosable.IsOpen => root != null && root.activeSelf;

        void IEscapeClosable.Close() => Hide();

        private void Refresh()
        {
            // 사라진 모험가(풀로 돌아감·씬에서 지워짐)의 창은 닫는다.
            if (_target == null)
            {
                Hide();
                return;
            }

            var model = _presenter.Build(_target);
            SetText(title, model.Title);
            SetText(state, model.State);
            SetText(hp, model.Hp);
            SetText(attack, model.Attack);
            SetText(purpose, model.Purpose);
            SetText(party, model.Party);
            SetText(mood, model.Mood);
            SetText(satisfaction, model.Satisfaction);

            effectsTitle.gameObject.SetActive(model.EffectCount > 0);
            for (var i = 0; i < effects.Length; i++)
            {
                var shown = i < model.EffectCount;
                effects[i].gameObject.SetActive(shown);
                if (shown)
                    SetText(effects[i], model.EffectAt(i));
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            value ??= string.Empty;
            if (label != null && label.text != value)
                label.text = value;
        }
    }
}
