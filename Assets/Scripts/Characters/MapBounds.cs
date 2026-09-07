using UnityEngine;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 캐릭터가 "정처 없이 돌아다닐" 때(할당된 기물이 없을 때) 벗어나면 안 되는 맵 범위.
    /// TestSceneBootstrap 등 씬 부트스트랩 쪽에서 배경/카메라 크기에 맞춰 SetBounds로 설정해준다.
    /// 아무도 설정 안 해주면(예: 기존 씬) 넉넉한 기본값으로 동작해서 이전 동작을 깨지 않는다.
    /// </summary>
    public static class MapBounds
    {
        public static Vector2 Min = new Vector2(-100f, -100f);
        public static Vector2 Max = new Vector2(100f, 100f);

        public static void SetBounds(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>맵 범위 안의 랜덤한 한 점 (z는 호출측 값 유지).</summary>
        public static Vector3 RandomPoint(float z = 0f)
        {
            float x = Random.Range(Min.x, Max.x);
            float y = Random.Range(Min.y, Max.y);
            return new Vector3(x, y, z);
        }

        /// <summary>맵 밖으로 나간 위치를 경계 안으로 밀어넣는다.</summary>
        public static Vector3 Clamp(Vector3 pos)
        {
            pos.x = Mathf.Clamp(pos.x, Min.x, Max.x);
            pos.y = Mathf.Clamp(pos.y, Min.y, Max.y);
            return pos;
        }
    }
}
