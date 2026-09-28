namespace Yoegoe.Core
{
    /// <summary>
    /// 요괴 말풍선(혼잣말·임시 대사) 일시 차단.
    /// 출석 윷점 옥토끼 대사가 끝나기 전에는 요괴들이 말하지 않는다 (기획 18장).
    /// </summary>
    public static class SpeechGate
    {
        public static bool YokaiSilenced { get; private set; }

        public static void Silence() => YokaiSilenced = true;
        public static void Release() => YokaiSilenced = false;
    }
}
