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
        public float Intimacy;   // 0~100, 넋은 0 고정
        public float Stamina;    // 혼: 0~(20+친밀도), 넋: 0~100(진화)
        public ActionState State = ActionState.Walking;
        public float StateTimer; // 놀기 5분 / 기력0 놀기 18시간→기절
    }
}
