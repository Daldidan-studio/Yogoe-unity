using System;
using System.Collections;
using System.Collections.Generic;
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
    /// YutThrowRoller로 진짜 랜덤 판정을 돌린다.
    ///
    /// 보유 요괴 전체가 각자 말 하나씩 판 위에 동시에 있다(YokaiInstance.BoardNode에 위치 저장).
    /// 던질 때마다 그 결과를 보유 요괴 전체에 적용했을 때의 후보 도착칸을 반짝여서 보여주고,
    /// 유저가 그중 하나(말/칸)를 직접 탭해서 골라야 그 말이 실제로 이동한다 — 업기/잡기는 다음 단계.
    /// </summary>
    public class NurtureTrainingController : MonoBehaviour
    {
        GameState _state;
        ScrollScreenUI _ui;
        bool _active;
        bool _throwFlag;
        bool _leaveFlag;
        string _pickedYokaiId;

        public void Bind(GameState state, ScrollScreenUI ui)
        {
            _state = state;
            _ui = ui;
            _ui.OnTrainingPressed += HandleTrainingPressed;
            _ui.YutGame.OnThrowPressed += HandleThrowPressed;
            _ui.YutGame.OnLeavePressed += HandleLeavePressed;
            _ui.YutGame.OnCandidateTapped += HandleCandidateTapped;
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

        void HandleCandidateTapped(string yokaiId)
        {
            if (_active) _pickedYokaiId = yokaiId;
        }

        IEnumerator RunSession()
        {
            _active = true;

            _state.ScrollMode = ScrollMode.Training;
            _ui.SetTrainingButtonVisible(false);
            _ui.YutGame.Show();
            RefreshYokaiPieces();
            _ui.RefreshAll(_state);

            bool freeThrow = false;
            while (true)
            {
                if (!freeThrow && _state.Wallet.Hearts <= 0)
                {
                    _ui.YutGame.SetThrowVisible(false);
                    _ui.YutGame.SetLeaveVisible(true);
                    _ui.ShowStatus("하트가 없어요");
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

                // 보유 요괴 전체의 후보 도착칸을 반짝이는 채로, 유저가 그중 하나를 탭해서
                // 고를 때까지 기다린다 — 실제로 이동하는 건 고른 요괴 하나뿐.
                yield return WaitCandidatePick(outcome.Result);
                var yokai = FindYokai(_pickedYokaiId) ?? _state.FocusYokai;

                var path = YutMoveResolver.GetPath(yokai.BoardNode, outcome.Result);
                // 빽도(뒤로 이동)는 참으로 되돌아가도 완주가 아니라 그냥 그 자리에 서는 것 — 전진일 때만 완주 판정
                bool finished = outcome.Result != YutThrowResult.Baekdo && TryTruncateAtStart(path, out path);

                yield return MovePiece(yokai, path, 0.32f);
                yokai.BoardNode = path[^1];

                yokai.AddEnergy(GameConstants.YutMoveEnergyGain);
                yokai.AddIntimacy(GameConstants.YutMoveIntimacyGain);
                if (finished)
                {
                    _state.Wallet.Coins += 1;
                    _ui.ShowStatus("완주! 엽전 +1");
                }
                _ui.RefreshAll(_state);
                // 던지기마다 하트·엽전·기력이 바뀌므로, 그 자리에서 바로 저장해서
                // 세션 도중 앱이 강종돼도 이번 던지기까지의 보상은 남게 한다.
                SaveService.Save(_state);

                if (outcome.GrantsBonusThrow)
                    freeThrow = true;
            }

            _state.ScrollMode = ScrollMode.Nurture;
            _ui.YutGame.Hide();
            _ui.YutGame.SetThrowVisible(false);
            _ui.YutGame.SetLeaveVisible(false);
            _ui.SetTrainingButtonVisible(true);
            _ui.RefreshAll(_state);
            SaveService.Save(_state);
            _active = false;
        }

        YokaiInstance FindYokai(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var y in _state.OwnedYokai)
                if (y.Id == id) return y;
            return null;
        }

        /// <summary>보유 요괴 전체를 지금 위치대로 판 위에 동시에 표시(각자 색+이니셜로 구분).</summary>
        void RefreshYokaiPieces(YokaiInstance movingYokai = null, int movingNode = 0)
        {
            var pieces = new List<YutMiniGame.YokaiPieceInfo>();
            foreach (var y in _state.OwnedYokai)
            {
                int node = ReferenceEquals(y, movingYokai) ? movingNode : y.BoardNode;
                pieces.Add(new YutMiniGame.YokaiPieceInfo(y.Id, y.DisplayName, node));
            }
            _ui.YutGame.ShowYokaiPieces(pieces);
        }

        /// <summary>
        /// 이번 던지기 결과를 보유 요괴 전체(각자 현재 위치)에 적용한 후보 도착칸을 반짝이며 보여주고,
        /// 유저가 그중 하나를 탭해서 고를 때까지 기다린다.
        /// </summary>
        IEnumerator WaitCandidatePick(YutThrowResult result)
        {
            var candidates = new List<YutMiniGame.YokaiMoveCandidate>();
            foreach (var y in _state.OwnedYokai)
            {
                var path = YutMoveResolver.GetPath(y.BoardNode, result);
                candidates.Add(new YutMiniGame.YokaiMoveCandidate(y.Id, y.DisplayName, path[^1]));
            }
            _ui.YutGame.FlashCandidates(candidates);
            _ui.ShowStatus(candidates.Count > 1 ? "움직일 말을 탭하세요" : "말을 탭해서 이동하세요");

            _pickedYokaiId = null;
            while (_pickedYokaiId == null) yield return null;

            _ui.YutGame.ClearCandidates();
        }

        IEnumerator MovePiece(YokaiInstance yokai, int[] path, float stepDuration)
        {
            RefreshYokaiPieces(yokai, path[0]);
            for (int i = 1; i < path.Length; i++)
            {
                yield return new WaitForSecondsRealtime(stepDuration);
                RefreshYokaiPieces(yokai, path[i]);
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
