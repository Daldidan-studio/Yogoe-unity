using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    // 플레이어 드래그(집어서 놓기)와 엔딩 기물 거절 리액션.
    public partial class CharacterAgent
    {
        const float DragLiftScale = 1.12f;
        const float DragBesideDistance = 0.85f;
        const float RefuseDurationSeconds = 3f;
        const float RefuseFlipInterval = 0.2f;
        Vector3 dragScaleBefore;
        private bool isRefusing;
        private float refuseTimer;
        private float refuseFlipTimer;

        /// <summary>플레이어 드래그 시작 — 예약/점유를 풀고 AI를 멈춘다. 살짝 키워 들어올린 느낌을 낸다.</summary>
        public void BeginPlayerDrag()
        {
            if (!CanBeDraggedByPlayer) return;
            IsBeingDragged = true;
            isRefusing = false;
            ClearWalkDestination();
            LeaveCurrentProp();
            lastPosition = transform.position;
            dragScaleBefore = transform.localScale;
            transform.localScale = dragScaleBefore * DragLiftScale;
        }

        public void SetDragWorldPosition(Vector3 world)
        {
            if (!IsBeingDragged) return;
            world.z = transform.position.z;
            transform.position = MapBounds.Clamp(world);
            lastPosition = transform.position; // 드롭 직후 가짜 이동량으로 방향이 튀지 않게
        }

        /// <summary>
        /// 드래그 종료.
        /// 빈 기물 → 즉시 착석. 타 엔딩 기물 → 옆에 두고 3초 도리도리 후 걷기.
        /// 점유된 기물 → 옆에 내려놓음. 그 외 → 놀기.
        /// 주저앉기 중이면 지친 상태를 유지한 채 배치.
        /// </summary>
        public void EndPlayerDrag(PropSlot dropProp)
        {
            if (!IsBeingDragged) return;
            IsBeingDragged = false;
            transform.localScale = dragScaleBefore.sqrMagnitude > 0.0001f ? dragScaleBefore : transform.localScale;
            lastPosition = transform.position;

            if (Stats.State == ActionState.Slumped)
            {
                SettleSlumpedAfterDrag(dropProp);
                return;
            }

            if (dropProp != null && dropProp.IsForbiddenEndingFor(this))
            {
                PlaceBesideProp(dropProp, startWalking: false);
                BeginRefuseShake();
                return;
            }

            if (dropProp != null && !dropProp.IsOccupied && TrySitOnProp(dropProp))
                return;

            if (dropProp != null && dropProp.IsOccupied && dropProp.CanBeUsedBy(this))
            {
                PlaceBesideProp(dropProp);
                return;
            }

            EnterPlaying();
        }

        /// <summary>기물 옆에 내려놓는다. startWalking이면 빈 기물 탐색으로 이어간다.</summary>
        void PlaceBesideProp(PropSlot prop, bool startWalking = true)
        {
            ClearWalkDestination();
            LeaveCurrentProp();
            isWandering = false;
            wanderTarget = null;

            Vector3 origin = prop.transform.position;
            Vector3 dir = Vector3.right;
            if (prop.Occupant != null)
            {
                Vector3 away = transform.position - prop.Occupant.transform.position;
                if (away.sqrMagnitude < 0.0001f)
                    away = Random.value < 0.5f ? Vector3.left : Vector3.right;
                dir = away.normalized;
            }
            else
            {
                Vector3 away = transform.position - origin;
                if (away.sqrMagnitude > 0.0001f)
                    dir = away.normalized;
                else
                    dir = Random.value < 0.5f ? Vector3.left : Vector3.right;
            }

            Vector3 beside = origin + dir * DragBesideDistance;
            beside.z = transform.position.z;
            transform.position = MapBounds.Clamp(beside);
            lastPosition = transform.position;
            if (startWalking) EnterWalking();
        }

        /// <summary>타 엔딩 기물 거절: 좌우 도리도리 후 걷기.</summary>
        void BeginRefuseShake()
        {
            isRefusing = true;
            refuseTimer = RefuseDurationSeconds;
            refuseFlipTimer = 0f;
            facing = FacingDir.Left;
            animFrame = 0;
            animTimer = 0f;
            ApplyRefuseFacingSprite();
        }

        void TickRefuse(float dt)
        {
            refuseTimer -= dt;
            refuseFlipTimer += dt;
            if (refuseFlipTimer >= RefuseFlipInterval)
            {
                refuseFlipTimer = 0f;
                facing = facing == FacingDir.Left ? FacingDir.Right : FacingDir.Left;
                ApplyRefuseFacingSprite();
            }

            if (refuseTimer > 0f) return;

            isRefusing = false;
            EnterWalking();
        }

        void ApplyRefuseFacingSprite()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null || Data == null) return;

            Sprite[] frames = WalkFramesForFacing(out bool flipX);
            if (frames == null || frames.Length == 0)
            {
                frames = Data.idle;
                flipX = facing == FacingDir.Left;
            }
            if (frames == null || frames.Length == 0) return;

            if (frames[0] != null)
                spriteRenderer.sprite = frames[0];
            spriteRenderer.flipX = flipX;
        }

        /// <summary>주저앉기 드래그 드롭: 상태·12시간 타이머 유지, 가능하면 기물 점유. 점유·타 엔딩이면 옆에.</summary>
        private void SettleSlumpedAfterDrag(PropSlot dropProp)
        {
            if (dropProp != null
                && dropProp.CanBeUsedBy(this)
                && !dropProp.IsOccupied
                && dropProp.TryOccupy(this))
            {
                currentProp = dropProp;
                var p = dropProp.transform.position;
                transform.position = new Vector3(p.x, p.y, transform.position.z);
                return;
            }

            if (dropProp != null && (dropProp.IsOccupied && dropProp.CanBeUsedBy(this) || dropProp.IsForbiddenEndingFor(this)))
            {
                Vector3 origin = dropProp.transform.position;
                Vector3 away = transform.position - (dropProp.Occupant != null
                    ? dropProp.Occupant.transform.position
                    : origin);
                if (away.sqrMagnitude < 0.0001f)
                    away = Random.value < 0.5f ? Vector3.left : Vector3.right;
                Vector3 beside = origin + away.normalized * DragBesideDistance;
                beside.z = transform.position.z;
                transform.position = MapBounds.Clamp(beside);
            }
            // 기물 아니면 드롭 좌표에 그대로 주저앉음 (State는 이미 Slumped)
        }
    }
}
