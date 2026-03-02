using _01.Code.Enemies;

namespace _01.Code.Entities
{
    public interface IRenderer
    {
        float FacingDirection { get; }
        void PlayClip(int clipHash, int layer = -1, float normalPosition = float.NegativeInfinity);
        void SetBool(ParamSO param, bool value);
        void SetFloat(ParamSO param, float value);
        void SetInt(ParamSO param, int value);
        void SetTrigger(ParamSO param);
        void FlipController(float xMove);
    }
}