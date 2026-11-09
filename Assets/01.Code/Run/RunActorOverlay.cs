using Code.UI;
using TMPro;
using UnityEngine;

namespace Code.Run
{
    /// <summary>
    /// 인물 머리 위·발밑 표시: 체력 막대, 스킬 준비 게이지, 적 목표 방 글, 노림 표시, 고른 부하 표시, 떠오르는 피해 숫자.
    /// 디렉터가 실행 중에 붙여 만든다(그림은 단순 도형과 글자).
    /// </summary>
    public class RunActorOverlay : MonoBehaviour
    {
        private float _headHeight = 1.05f;

        private Transform _barFill;
        private GameObject _skillBack;
        private Transform _skillFill;
        private TextMeshPro _intent;
        private SpriteRenderer _targetMark;
        private SpriteRenderer _selectMark;
        private Sprite _square;
        private TextMeshPro _pop;
        private float _popSeconds;
        private Color _popColor;

        /// <summary>표시를 만든다. 위치는 인물 로컬 좌표(배율 적용 전)다.</summary>
        public void Build(Sprite square, bool isMinion, float headHeight, Sprite dot, Color affinity)
        {
            _square = square;
            _headHeight = headHeight;
            Quad("HealthBack", new Vector3(0f, _headHeight, 0f), new Vector2(0.62f, 0.07f), HudPalette.Track, 30);
            SpriteRenderer fill = Quad("HealthFill", new Vector3(0f, _headHeight, 0f), new Vector2(0.6f, 0.05f),
                isMinion ? HudPalette.Gain : HudPalette.Danger, 31);
            _barFill = fill.transform;

            _skillBack = Quad("SkillBack", new Vector3(0f, _headHeight + 0.09f, 0f), new Vector2(0.62f, 0.05f), HudPalette.Track, 30).gameObject;
            _skillFill = Quad("SkillFill", new Vector3(0f, _headHeight + 0.09f, 0f), new Vector2(0.6f, 0.03f), HudPalette.Warn, 31).transform;
            SetSkill(-1f);

            _intent = Label("Intent", new Vector3(0f, _headHeight + 0.2f, 0f), 2.6f, HudPalette.Accent);
            _targetMark = Quad("TargetMark", new Vector3(0f, _headHeight + 0.3f, 0f), new Vector2(0.16f, 0.16f), HudPalette.Danger, 32);
            _targetMark.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _targetMark.gameObject.SetActive(false);
            _selectMark = Quad("SelectMark", new Vector3(0f, 0.01f, 0f), new Vector2(0.7f, 0.08f), HudPalette.Accent, 19);
            _selectMark.gameObject.SetActive(false);
            SpriteRenderer affinityDot = Quad("Affinity", new Vector3(-0.38f, _headHeight * 0.55f, 0f), new Vector2(0.16f, 0.16f), affinity, 34);
            affinityDot.sprite = dot;
            _pop = Label("Pop", new Vector3(0.18f, _headHeight + 0.1f, 0f), 2.8f, HudPalette.Loss);
            _pop.SetText("+0000");
            _pop.gameObject.SetActive(false);
        }

        public void SetHealth(float ratio)
        {
            if (_barFill == null) return;
            ratio = Mathf.Clamp01(ratio);
            _barFill.localScale = new Vector3(0.6f * ratio, 0.05f, 1f);
            _barFill.localPosition = new Vector3(-0.3f * (1f - ratio), _headHeight, 0f);
        }

        /// <summary>스킬 준비 게이지(0~1, 1이면 준비됨). 음수면 숨긴다.</summary>
        public void SetSkill(float ratio)
        {
            if (_skillBack == null) return;
            bool visible = ratio >= 0f;
            _skillBack.SetActive(visible);
            _skillFill.gameObject.SetActive(visible);
            if (!visible) return;
            ratio = Mathf.Clamp01(ratio);
            _skillFill.localScale = new Vector3(0.6f * ratio, 0.03f, 1f);
            _skillFill.localPosition = new Vector3(-0.3f * (1f - ratio), _headHeight + 0.09f, 0f);
        }

        /// <summary>머리 위 글(적의 목표 방). 이웃과 겹치면 raise로 한 줄 위에 띄운다.</summary>
        public void SetIntent(string text, bool raise = false)
        {
            if (_intent == null) return;
            _intent.text = text ?? string.Empty;
            _intent.transform.localPosition = new Vector3(0f, _headHeight + (raise ? 0.75f : 0.3f), 0f);
        }

        public void SetTargeted(bool targeted)
        {
            if (_targetMark != null) _targetMark.gameObject.SetActive(targeted);
        }

        public void SetSelected(bool selected)
        {
            if (_selectMark != null) _selectMark.gameObject.SetActive(selected);
        }

        /// <summary>피해(음수)·회복(양수) 숫자를 머리 위로 띄웠다 지운다.</summary>
        public void PopNumber(int amount)
        {
            if (amount == 0) return;
            _popColor = amount < 0 ? HudPalette.Loss : HudPalette.Gain;
            _pop.SetText(amount > 0 ? "+{0}" : "{0}", (float)amount);
            _pop.color = _popColor;
            _popSeconds = 0.8f;
            _pop.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_popSeconds <= 0f || _pop == null) return;
            _popSeconds = Mathf.Max(0f, _popSeconds - Time.unscaledDeltaTime);
            float progress = 1f - _popSeconds / 0.8f;
            _pop.transform.localPosition = new Vector3(0.18f, _headHeight + 0.1f + progress * 0.5f, 0f);
            Color color = _popColor;
            color.a = Mathf.Clamp01(_popSeconds / 0.6f);
            _pop.color = color;
            if (_popSeconds <= 0f) _pop.gameObject.SetActive(false);
        }

        private SpriteRenderer Quad(string objectName, Vector3 local, Vector2 size, Color color, int order)
        {
            GameObject quad = new(objectName);
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = local;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = quad.AddComponent<SpriteRenderer>();
            renderer.sprite = _square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private TextMeshPro Label(string objectName, Vector3 local, float size, Color color)
        {
            GameObject go = new(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.sortingOrder = 40;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.2f;
            text.outlineColor = HudPalette.Deep;
            text.raycastTarget = false;
            return text;
        }
    }
}
