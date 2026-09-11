using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    // 방향별 4프레임 걷기 애니메이션 스와핑 + 상태별 스프라이트 선택.
    public partial class CharacterAgent
    {
        private enum FacingDir { Down, Up, Left, Right }
        private const float AnimFrameInterval = 0.15f; // 프레임당 재생 시간
        private const float FacingMoveThresholdSq = 0.00004f; // ~0.006m/frame @60fps
        private const float FacingAxisBias = 1.25f; // 축이 이만큼 우세할 때만 좌우↔상하 전환

        private Vector3 lastPosition;
        private FacingDir facing = FacingDir.Down;
        private int animFrame;
        private float animTimer;

        private void ApplyAnimationFrameImmediate()
        {
            if (spriteRenderer == null || Data == null) return;
            // 넋은 불꽃 스프라이트를 유지 — 혼 걷기 시트로 덮어쓰지 않는다
            if (Stats.Stage == GrowthStage.Neok) return;
            bool wantsWalk = Stats.State == ActionState.Walking
                || Stats.State == ActionState.Playing;
            Sprite[] frames = ResolveAnimationFrames(wantsWalk, out bool flipX);
            if (frames != null && frames.Length > 0 && frames[0] != null)
                spriteRenderer.sprite = frames[0];
            spriteRenderer.flipX = flipX;
        }

        /// <summary>
        /// 이번 프레임 이동·상태로 스프라이트를 재생한다.
        /// 걷기/놀기 이동: 시트 1~4행(walkDown/Left/Right/Up).
        /// 놀기 정지: idle. 머물기/주저앉기/기절: stay/slumped/fainted.
        /// </summary>
        private void UpdateWalkAnimation(float dt)
        {
            if (Stats.Stage == GrowthStage.Neok) return;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null || Data == null) return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            bool moved = delta.sqrMagnitude > FacingMoveThresholdSq;

            // 이동 중이거나, 목적지/방황 목표가 있으면 걷기 사이클(시트 1~4행)
            bool wantsWalkCycle =
                Stats.State == ActionState.Walking
                || (Stats.State == ActionState.Playing && (moved || wanderTarget.HasValue));

            if (moved)
                UpdateFacingFromDelta(delta);

            bool advanceFrames = wantsWalkCycle || Stats.State == ActionState.Staying
                || Stats.State == ActionState.Slumped
                || Stats.State == ActionState.Fainted
                || Stats.State == ActionState.Playing;

            Sprite[] frames = ResolveAnimationFrames(wantsWalkCycle, out bool flipX);
            if (frames == null || frames.Length == 0) return;

            if (advanceFrames)
            {
                float interval = wantsWalkCycle ? AnimFrameInterval : AnimFrameInterval * 2f;
                animTimer += dt;
                if (animTimer >= interval)
                {
                    animTimer -= interval;
                    animFrame = (animFrame + 1) % frames.Length;
                }
            }
            else
            {
                animFrame = 0;
                animTimer = 0f;
            }

            if (animFrame >= frames.Length)
                animFrame = 0;

            int idx = animFrame;
            if (frames[idx] != null)
                spriteRenderer.sprite = frames[idx];
            spriteRenderer.flipX = flipX;
        }

        private void UpdateFacingFromDelta(Vector3 delta)
        {
            float ax = Mathf.Abs(delta.x);
            float ay = Mathf.Abs(delta.y);
            // 분리 밀림 등으로 축이 매 프레임 바뀌면 스프라이트가 깜빡이듯 끊긴다.
            if (facing == FacingDir.Left || facing == FacingDir.Right)
            {
                if (ay > ax * FacingAxisBias)
                    facing = delta.y > 0f ? FacingDir.Up : FacingDir.Down;
                else if (ax >= ay)
                    facing = delta.x > 0f ? FacingDir.Right : FacingDir.Left;
            }
            else
            {
                if (ax > ay * FacingAxisBias)
                    facing = delta.x > 0f ? FacingDir.Right : FacingDir.Left;
                else if (ay >= ax)
                    facing = delta.y > 0f ? FacingDir.Up : FacingDir.Down;
            }
        }

        private Sprite[] ResolveAnimationFrames(bool wantsWalkCycle, out bool flipX)
        {
            flipX = false;
            if (wantsWalkCycle)
                return WalkFramesForFacing(out flipX);

            switch (Stats.State)
            {
                case ActionState.Staying:
                    if (HasFrames(Data.stay)) return Data.stay;
                    break;
                case ActionState.Playing:
                    if (HasFrames(Data.idle)) return Data.idle;
                    break;
                case ActionState.Slumped:
                    if (HasFrames(Data.slumped)) return Data.slumped;
                    break;
                case ActionState.Fainted:
                    if (HasFrames(Data.fainted)) return Data.fainted;
                    break;
            }

            if (HasFrames(Data.idle)) return Data.idle;
            return WalkFramesForFacing(out flipX);
        }

        private Sprite[] WalkFramesForFacing(out bool flipX)
        {
            flipX = false;
            Sprite[] frames;
            switch (facing)
            {
                case FacingDir.Up:
                    frames = Data.walkUp;
                    break;
                case FacingDir.Right:
                    frames = Data.walkRight;
                    break;
                case FacingDir.Left:
                    // 전용 Left가 없으면 Right를 좌우반전해서 사용
                    if (HasFrames(Data.walkLeft))
                    {
                        frames = Data.walkLeft;
                    }
                    else if (HasFrames(Data.walkRight))
                    {
                        flipX = true;
                        frames = Data.walkRight;
                    }
                    else
                    {
                        frames = Data.walkLeft;
                    }
                    break;
                default:
                    frames = Data.walkDown;
                    break;
            }

            if (HasFrames(frames)) return frames;
            if (HasFrames(Data.walkDown))
            {
                flipX = false;
                return Data.walkDown;
            }
            return frames;
        }

        private static bool HasFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) return true;
            return false;
        }
    }
}
