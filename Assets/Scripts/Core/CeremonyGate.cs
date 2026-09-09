namespace Yoegoe.Core
{
    /// <summary>소환·진화 연출 중 맵 입력 차단.</summary>
    public static class CeremonyGate
    {
        public static bool BlocksWorldInput { get; private set; }

        public static void Begin() => BlocksWorldInput = true;
        public static void End() => BlocksWorldInput = false;
    }
}
