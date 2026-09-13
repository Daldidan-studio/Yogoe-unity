#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Yoegoe.Minigames.Yut;
using Yoegoe.UI;

namespace Yoegoe.UI.EditorTools
{
    /// <summary>
    /// Play 중 실제 윷 던지기·이동 연출로 자동 플레이해 완주/특수칸/도전/이무기 QA UI를 확인한다.
    /// </summary>
    public static class YutFinishRewardSimMenu
    {
        const string MenuRoot = "Yoegoe/Yut QA/";
        const string SquareRoot = MenuRoot + "특수칸 Presenter/";
        const string ChallengeRoot = MenuRoot + "도전과제 Presenter/";

        [MenuItem(SquareRoot + "엽전 칸 → 받기/광고", false, 170)]
        static void SquareCoin() => RunSquare(YutBoardLayout.SpecialSquareKind.Coin);

        [MenuItem(SquareRoot + "공양물 칸 → 받기/광고", false, 171)]
        static void SquareOffering() => RunSquare(YutBoardLayout.SpecialSquareKind.Offering);

        [MenuItem(SquareRoot + "정화수 칸 → 받기/광고", false, 172)]
        static void SquarePurifiedWater() => RunSquare(YutBoardLayout.SpecialSquareKind.PurifiedWater);

        [MenuItem(SquareRoot + "보물상자 → 내용확인→받기/광고", false, 173)]
        static void SquareTreasure() => RunSquare(YutBoardLayout.SpecialSquareKind.Treasure);

        [MenuItem(ChallengeRoot + "모 2연속 → 보물상자×3", false, 175)]
        static void ChallengeMo() => RunChallenge(YutChallengeKind.ConsecutiveMo);

        [MenuItem(ChallengeRoot + "빽도 2연속 → 보물상자×3", false, 176)]
        static void ChallengeBaekdo() => RunChallenge(YutChallengeKind.ConsecutiveBaekdo);

        [MenuItem(ChallengeRoot + "미잡힘 넷 완주 → 보물상자×3", false, 177)]
        static void ChallengeFinishAll() => RunChallenge(YutChallengeKind.FinishAllUncaptured);

        [MenuItem(MenuRoot + "이무기 잡기→참 아래 대기까지", false, 181)]
        static void CaptureImugiWaiting()
        {
            var screen = YutScreen.Instance;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("Yut QA",
                    "Play 중 YutScreen.Instance가 없습니다. 메인 씬에서 Play한 뒤 다시 실행하세요.", "OK");
                return;
            }

            screen.DebugStartCaptureImugiQa();
        }

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

        [MenuItem(SquareRoot + "엽전 칸 → 받기/광고", true)]
        [MenuItem(SquareRoot + "공양물 칸 → 받기/광고", true)]
        [MenuItem(SquareRoot + "정화수 칸 → 받기/광고", true)]
        [MenuItem(SquareRoot + "보물상자 → 내용확인→받기/광고", true)]
        [MenuItem(ChallengeRoot + "모 2연속 → 보물상자×3", true)]
        [MenuItem(ChallengeRoot + "빽도 2연속 → 보물상자×3", true)]
        [MenuItem(ChallengeRoot + "미잡힘 넷 완주 → 보물상자×3", true)]
        [MenuItem(MenuRoot + "이무기 잡기→참 아래 대기까지", true)]
        [MenuItem(MenuRoot + "자동 플레이 시작 (끝까지)", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×1 골인 후 그만", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×2 골인 후 그만", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×3 골인 후 그만", true)]
        [MenuItem(MenuRoot + "자동 플레이 ×4 (광고 선택 목표)", true)]
        [MenuItem(MenuRoot + "자동 플레이 정지", true)]
        static bool Validate() => Application.isPlaying;

        static void RunSquare(YutBoardLayout.SpecialSquareKind kind)
        {
            var screen = YutScreen.Instance;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("Yut QA",
                    "Play 중 YutScreen.Instance가 없습니다. 메인 씬에서 Play한 뒤 다시 실행하세요.", "OK");
                return;
            }

            screen.DebugStartSquareRewardQa(kind);
        }

        static void RunChallenge(YutChallengeKind kind)
        {
            var screen = YutScreen.Instance;
            if (screen == null)
            {
                EditorUtility.DisplayDialog("Yut QA",
                    "Play 중 YutScreen.Instance가 없습니다. 메인 씬에서 Play한 뒤 다시 실행하세요.", "OK");
                return;
            }

            screen.DebugStartChallengeQa(kind);
        }

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
