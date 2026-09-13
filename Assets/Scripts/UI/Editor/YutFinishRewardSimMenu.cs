#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// Play 중 실제 윷 던지기·이동 연출로 자동 플레이해 완주 보상 UI를 확인한다.
    /// </summary>
    public static class YutFinishRewardSimMenu
    {
        const string MenuRoot = "Yoegoe/Yut QA/";

        [MenuItem(MenuRoot + "자동 플레이 시작 (끝까지)", false, 190)]
        static void StartUntilEnd() => RunAuto(0);

        [MenuItem(MenuRoot + "자동 플레이 ×1 골인 후 그만", false, 200)]
        static void Sim1() => RunAuto(1);

        [MenuItem(MenuRoot + "자동 플레이 ×2 골인 후 그만", false, 201)]
        static void Sim2() => RunAuto(2);

        [MenuItem(MenuRoot + "자동 플레이 ×3 골인 후 그만", false, 202)]
        static void Sim3() => RunAuto(3);

        [MenuItem(MenuRoot + "자동 플레이 ×4 (광고 선택 목표)", false, 203)]
        static void Sim4() => RunAuto(4);

        [MenuItem(MenuRoot + "자동 플레이 정지", false, 210)]
        static void Stop()
        {
            var screen = YutScreen.Instance;
            if (screen != null) screen.DebugStopAutoPlay();
        }

        [MenuItem(MenuRoot + "자동 플레이 시작 (끝까지)", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×1 골인 후 그만", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×2 골인 후 그만", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×3 골인 후 그만", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×4 (광고 선택 목표)", true)]
        [MenuItem(MenuRoot + "자동 플레이 정지", true)]
        static bool Validate() => Application.isPlaying;

        static void RunAuto(int stopAtStack)
        {
            var screen = YutScreen.Instance;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("Yut QA",
                    "Play 중 YutScreen.Instance가 없습니다. 메인 씬에서 Play한 뒤 다시 실행하세요.", "OK");
                return;
            }

            screen.DebugStartAutoPlay(stopAtStack);
        }
    }
}
#endif
