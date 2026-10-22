using UnityEngine;

namespace Code.Entities
{
    /// <summary>
    /// 캐릭터 몸통의 좌우 회전(Y 0/180)과 흔들림(z 기울기)을 나눠 읽는 규칙.
    /// 흔들림 연출은 자기 시작 회전으로 되돌리며 Y를 덮어쓰므로, 방향을 다시 얹을 때 기울기만 살려야 한다.
    /// </summary>
    public static class CharacterFacingRules
    {
        /// <summary>
        /// 회전 Ry(0|180)·Rz(z)의 위쪽·앞쪽 벡터로 z 기울기(도)를 구한다. Y가 180이면 앞쪽이 -z이고 위쪽 x 부호가 뒤집힌다.
        /// </summary>
        public static float TiltZ(Vector3 up, Vector3 forward)
        {
            var turned = forward.z < 0f;
            var x = turned ? up.x : -up.x;
            return Mathf.Atan2(x, up.y) * Mathf.Rad2Deg;
        }
    }
}
