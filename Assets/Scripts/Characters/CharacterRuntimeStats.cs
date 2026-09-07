using System;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    /// <summary>
    /// 런타임 상태값만 담는 클래스. CharacterData(기획 고정값)와 분리해서
    /// 나중에 세이브 파일에는 이 클래스만 직렬화하면 되게 구성.
    /// </summary>
    [Serializable]
    public class CharacterRuntimeStats
    {
        public GrowthStage Stage;
        public float Intimacy;   // 0~100, 넋은 미사용
        public float Stamina;    // 0~100
        public ActionState State = ActionState.Walking;
        public float StateTimer; // 현재 상태 진입 후 경과 시간(초) — 머물기 5분, 주저앉기 12시간 판정에 사용
    }
}
