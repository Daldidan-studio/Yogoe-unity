using System;
using System.Collections;
using KSpirits.Core;
using KSpirits.Minigames.Yut;
using KSpirits.Model;
using KSpirits.UI;
using UnityEngine;

namespace KSpirits.Systems
{
    /// <summary>
    /// 튜토리얼이 끝난 뒤(본게임, TutorialStep.Done) 수련장에서 실제로 플레이하는 윷놀이 루프.
    /// 튜토리얼의 StepTraining()은 빽도→도 두 번만 재생하는 스크립트고, 이쪽은
    /// YutThrowRoller로 진짜 랜덤 판정을 돌린다. 말은 아직 1개짜리 최소 루프
    /// (업기·잡기·상대 말 등은 다음 단계).
    /// </summary>
    public class NurtureTrainingController : MonoBehaviour
    {
        GameState _state;
        ScrollScreenUI _ui;
        int _pieceNode;
        bool _active;
        bool _throwFlag;
        bool _leaveFlag;

        public void Bind(GameState state, ScrollScreenUI ui)
        {
            _state = state;
            _ui = ui;
            _ui.OnTrainingPressed += HandleTrainingPressed;
            _ui.YutGame.OnThrowPressed += HandleThrowPressed;
            _ui.YutGame.OnLeavePressed += HandleLeavePressed;
        }

        void HandleTrainingPressed()
        {
            if (_active) return;
            if (_state.TutorialStep != TutorialStepId.Done) return; // 튜토리얼 중엔 TutorialController가 처리
            StartCoroutine(RunSession());
        }

        /// <summary>개발용: 버튼을 누르지 않고 바로 세션을 시작한다 (디버그 메뉴용).</summary>
        public void BeginSessionForDebug()
        {
            StopAllCoroutines();
            _active = false;
            StartCoroutine(RunSession());
        }

        void HandleThrowPressed()
        {
            if (_active) _throwFlag = true;
        }

        void HandleLeavePressed()
        {
            if (_active) _leaveFlag = true;
        }

        IEnumerator RunSession()
        {
            _active = true;
            _pieceNode = YutBoardLayout.Start;

            _state.ScrollMode = ScrollMode.Training;
            _ui.SetTrainingButtonVisible(false);
            _ui.YutGame.Show();
            _ui.YutGame.SetPieceIndex(_pieceNode);
            _ui.YutGame.ShowResult("윷을 던져보세요");
            _ui.RefreshAll(_state);

            bool freeThrow = false;
            while (true)
            {
                if (!freeThrow && _state.Wallet.Hearts <= 0)
                {
                    _ui.YutGame.SetThrowVisible(false);
                    _ui.YutGame.SetLeaveVisible(true);
                    _ui.YutGame.ShowResult("하트가 없어요");
                    yield return WaitLeave();
                    break;
                }

                _ui.YutGame.SetThrowVisible(true);
                _ui.YutGame.SetLeaveVisible(true);
                yield return WaitThrowOrLeave();
                if (_leaveFlag) break;

                if (!freeThrow)
                    _state.Wallet.TrySpendHearts(1);
                freeThrow = false;
                _ui.YutGame.SetThrowVisible(false);
                _ui.RefreshAll(_state);

                var outcome = YutThrowRoller.Roll();
                yield return _ui.YutGame.PlayThrowAnim(outcome.Result);

                var path = YutMoveResolver.GetPath(_pieceNode, outcome.Result);
                // 빽도(뒤로 이동)는 참으로 되돌아가도 완주가 아니라 그냥 그 자리에 서는 것 — 전진일 때만 완주 판정
                bool finished = outcome.Result != YutThrowResult.Baekdo && TryTruncateAtStart(path, out path);

                _ui.YutGame.ShowResult(finished
                    ? $"{outcome.Result.DisplayName()} → 골인!"
                    : outcome.GrantsBonusThrow
                        ? $"{outcome.Result.DisplayName()}! (한 번 더)"
                        : $"{outcome.Result.DisplayName()}!");

                yield return MovePiece(path, 0.32f);
                _pieceNode = path[^1];

                _state.FocusYokai.AddEnergy(GameConstants.YutMoveEnergyGain);
                _state.FocusYokai.AddIntimacy(GameConstants.YutMoveIntimacyGain);
                if (finished)
                {
                    _state.Wallet.Coins += 1;
                    _ui.ShowStatus("완주! 엽전 +1");
                }
                _ui.RefreshAll(_state);

                if (outcome.GrantsBonusThrow)
                    freeThrow = true;
            }

            _state.ScrollMode = ScrollMode.Nurture;
            _ui.YutGame.Hide();
            _ui.YutGame.SetThrowVisible(false);
            _ui.YutGame.SetLeaveVisible(false);
            _ui.SetTrainingButtonVisible(true);
            _ui.RefreshAll(_state);
            _active = false;
        }

        IEnumerator MovePiece(int[] path, float stepDuration)
        {
            _ui.YutGame.SetPieceIndex(path[0]);
            for (int i = 1; i < path.Length; i++)
            {
                yield return new WaitForSecondsRealtime(stepDuration);
                _ui.YutGame.SetPieceIndex(path[i]);
            }
        }

        IEnumerator WaitThrowOrLeave()
        {
            _throwFlag = false;
            _leaveFlag = false;
            while (!_throwFlag && !_leaveFlag) yield return null;
        }

        IEnumerator WaitLeave()
        {
            _leaveFlag = false;
            while (!_leaveFlag) yield return null;
        }

        /// <summary>경로 중간에 참(Start, 0)이 다시 나오면 그 자리에서 완주로 끊는다.</summary>
        static bool TryTruncateAtStart(int[] path, out int[] truncated)
        {
            for (int i = 1; i < path.Length; i++)
            {
                if (path[i] == YutBoardLayout.Start)
                {
                    var result = new int[i + 1];
                    Array.Copy(path, result, i + 1);
                    truncated = result;
                    return true;
                }
            }
            truncated = path;
            return false;
        }
    }
}
