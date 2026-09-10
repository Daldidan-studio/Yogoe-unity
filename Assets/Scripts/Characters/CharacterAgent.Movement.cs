using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    // 이동 상태머신 본체: Walking → Staying(생산) → [기력 0] Slumped → [12시간] Fainted, Playing(놀기).
    public partial class CharacterAgent
    {
        private const float PlayDurationSeconds = 5f * 60f; // 6-2 놀기
        private const float FaintThresholdSeconds = 12f * 60f * 60f;
        private const float WanderRetrySeconds = 30f;
        private const float SeparationRadius = 0.55f;
        /// <summary>전진을 죽이지 않도록 이동 속도보다 낮게 유지.</summary>
        private const float SeparationSpeed = 1.2f;

        private PropSlot currentProp;
        private PropSlot previousProp;
        private PropSlot destination;
        private bool isWandering;
        private float wanderTimer;
        private Vector3? wanderTarget; // isWandering 중 실제로 걸어갈 맵 안의 임시 목적지

        /// <summary>
        /// 복귀 catch-up 중 Walking이면 목적지만 보장한다.
        /// 위치 스냅은 하지 않는다 — 화면이 보이는 상태에서 기물로 순간이동하면 UX가 깨진다.
        /// </summary>
        private void EnsureWalkingDestination()
        {
            if (isWandering || destination == null)
                PickDestination();
        }

        // ---------------- Walking ----------------

        private void EnterWalking()
        {
            Stats.State = ActionState.Walking;
            Stats.StateTimer = 0f;
            animFrame = 0;
            animTimer = 0f;
            lastPosition = transform.position;
            PickDestination();
        }

        private void PickDestination()
        {
            // 이전에 찜해둔 목적지가 있으면(도착 못 하고 재추첨하는 경우) 먼저 예약 해제.
            if (destination != null) destination.ReleaseReservation(this);

            destination = PropManager.Instance != null
                ? PropManager.Instance.GetRandomAvailableProp(this, previousProp)
                : null;

            // 고르는 즉시 찜해둬서, 같은 프레임에 다른 캐릭터가 고를 때 후보에서 빠지게 한다
            // (다 같이 같은 기물로 몰려가는 문제 방지).
            if (destination != null) destination.TryReserve(this);

            isWandering = destination == null;
            wanderTimer = 0f;
            wanderTarget = null;
        }

        private void TickWalking(float dt)
        {
            if (isWandering)
            {
                // 6-2 방황/재추첨 — Docs/06_행동룰.md (후보없음·자리없음을 한 루프로 통합)
                wanderTimer += dt;
                if (wanderTimer >= WanderRetrySeconds) { PickDestination(); return; }

                if (wanderTarget == null || Vector3.Distance(transform.position, wanderTarget.Value) < 0.05f)
                {
                    wanderTarget = MapBounds.RandomPoint(transform.position.z);
                }
                transform.position = MapBounds.Clamp(Vector3.MoveTowards(transform.position, wanderTarget.Value, moveSpeed * dt));
                ResolveSeparation(dt);
                return;
            }

            if (destination == null) { PickDestination(); return; }

            Vector3 targetPos = destination.transform.position;
            transform.position = MapBounds.Clamp(Vector3.MoveTowards(transform.position, targetPos, moveSpeed * dt));
            ResolveSeparation(dt);

            if (Vector3.Distance(transform.position, targetPos) < 0.05f)
            {
                if (destination.TryOccupy(this))
                {
                    currentProp = destination;
                    destination = null;
                    EnterStaying();
                }
                else
                {
                    // 6-2 "밀린 쪽은 되돌지 않고 직진해 다음 기물로" — 실제 경로 기하가 없어서
                    // 다음 후보를 즉시 재선정하는 것으로 단순화
                    PickDestination();
                }
            }
        }

        /// <summary>
        /// 걷는 중인 캐릭터끼리 너무 가까워지면 서로 살짝 밀어낸다.
        /// 상대가 앉아 있는 경우만 피하고, 전진 방향 성분은 약하게 유지해 제자리 떨림을 막는다.
        /// </summary>
        private void ResolveSeparation(float dt)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                var other = ActiveAgents[i];
                if (other == null || other == this) continue;
                if (other.Stats.Stage == GrowthStage.Neok) continue;
                // 앉아/기절한 상대는 밀되, 같이 걷는 상대만 상호 분리(앉아있는 요괴 자리는 건드리지 않음)
                if (other.Stats.State == ActionState.Fainted) continue;

                Vector3 diff = transform.position - other.transform.position;
                diff.z = 0f;
                float dist = diff.magnitude;
                if (dist > 0.0001f && dist < SeparationRadius)
                    push += diff.normalized * (SeparationRadius - dist);
            }

            if (push == Vector3.zero) return;

            // 한 프레임 밀림 상한 — moveSpeed 대비 과도하면 전진이 상쇄되어 끊겨 보인다.
            float maxPush = moveSpeed * 0.55f * dt;
            Vector3 step = push * SeparationSpeed * dt;
            if (step.sqrMagnitude > maxPush * maxPush)
                step = step.normalized * maxPush;

            transform.position = MapBounds.Clamp(transform.position + step);
        }

        // ---------------- Staying ----------------

        private void EnterStaying()
        {
            Stats.State = ActionState.Staying;
            Stats.StateTimer = 0f;
            // 머물기 중엔 기물 요구 ?를 숨긴다. 요구 기물이면 3초 체류만 내부 누적.
            Requests?.NotifySatOnProp(currentProp);
        }

        private void TickStaying(float dt)
        {
            // 긴 dt에서도 기력 고갈 전 구간만 생산 (OfflineSimulator와 동일).
            float left = dt;
            int guard = 0;
            while (left > 0.0001f && Stats.State == ActionState.Staying && guard++ < 8)
                left -= TickStayingSlice(left);
        }

        /// <summary>
        /// 머물기 한 구간. 소모한 초를 반환한다.
        /// 생산은 앉아 있는 동안 기력 0까지 계속(상한 없음). 5분·33분치 강제 종료 없음.
        /// </summary>
        private float TickStayingSlice(float dt)
        {
            const float drain = 1f / 20f; // 6-2: 1분당 3 = 20초당 1
            float timeToZero = Stats.Stamina > 0f ? Stats.Stamina / drain : 0f;
            float slice = Mathf.Min(dt, timeToZero);

            if (slice <= 0f)
            {
                Stats.Stamina = 0f;
                EnterSlumped();
                return 0.0001f;
            }

            float staminaBefore = Stats.Stamina;
            Stats.StateTimer += slice;
            Stats.Stamina -= slice * drain;
            if (Stats.Stamina < 0f) Stats.Stamina = 0f;
            Requests.NotifyStaminaDrain(staminaBefore, Stats.Stamina);

            if (currentProp != null)
            {
                double perMinute = currentProp.GetBaseProductionThisLevel()
                                    * GetIntimacyCorrection()
                                    * GetEndingPropCorrection();
                currentProp.AddToMeritPile(perMinute / 60.0 * slice);
            }

            if (Stats.Stamina <= 0f)
            {
                Stats.Stamina = 0f;
                EnterSlumped();
            }

            return slice;
        }

        /// <summary>7-1: 머물기·생산 중일 때만 분당 생산량. 상점 리셋 비용용.</summary>
        public double GetProductionPerMinuteIfStaying()
        {
            if (Stats.Stage == GrowthStage.Neok) return 0;
            if (Stats.State != ActionState.Staying || currentProp == null) return 0;
            return currentProp.GetBaseProductionThisLevel()
                   * GetIntimacyCorrection()
                   * GetEndingPropCorrection();
        }

        /// <summary>7-1: 친밀도 보정 = 1 + 친밀도/100. 넋은 친밀도 없음 → ×1.</summary>
        private double GetIntimacyCorrection()
        {
            if (Stats.Stage == GrowthStage.Neok) return 1.0;
            return 1.0 + Stats.Intimacy / 100.0;
        }

        /// <summary>7-1: 주인이 자기 엔딩 기물에 앉으면 ×2 (MVP: 옥토끼–떡절구).</summary>
        private double GetEndingPropCorrection()
        {
            if (currentProp == null || currentProp.data == null || Data == null) return 1.0;
            bool ownEnding = currentProp.data.isEndingProp && currentProp.data.owner == Data.id;
            return ownEnding ? 2.0 : 1.0;
        }

        /// <summary>전용 점유 아트 표시 중에는 캐릭터 스프라이트를 숨긴다.</summary>
        public void SetSpriteVisible(bool visible)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = visible;
        }

        private void LeaveCurrentProp()
        {
            if (currentProp != null)
            {
                currentProp.Vacate(this);
                previousProp = currentProp; // 다음 목적지 선정 시 제외 대상
                currentProp = null;
                Requests?.NotifyLeftProp();
            }
            SetSpriteVisible(true);
        }

        // ---------------- Playing (놀기, 6-2) ----------------

        /// <summary>
        /// 놀기: 기물 점유를 풀고 5분간 맵을 돌아다닌다. 기력 소모·생산 없음.
        /// 진입: 기물 아닌 곳 드래그 드롭.
        /// 종료: 5분 후 Walking(목표 타겟팅) → 기물. Docs/06_행동룰.md
        /// </summary>
        public void EnterPlaying()
        {
            if (Stats.Stage == GrowthStage.Neok) return;
            if (Stats.State == ActionState.Fainted) return;

            bool fromStay = Stats.State == ActionState.Staying;

            ClearWalkDestination();
            LeaveCurrentProp();
            isWandering = false;
            wanderTarget = null;
            Stats.State = ActionState.Playing;
            Stats.StateTimer = 0f;
            animFrame = 0;
            animTimer = 0f;
            lastPosition = transform.position;

            // 10장 기물 요구: 머물다 일어남(놀기 진입)
            if (fromStay) Requests.TryStartPropRequest();
        }

        private void TickPlaying(float dt)
        {
            Stats.StateTimer += dt;
            if (Stats.StateTimer >= PlayDurationSeconds)
            {
                EnterWalking();
                return;
            }

            // 목적지 없이 맵을 떠돈다. 가끔 한곳에 멈춰 쉬는 연출은 wander 도착 시 짧은 대기로 대체.
            if (wanderTarget == null || Vector3.Distance(transform.position, wanderTarget.Value) < 0.05f)
            {
                // 도착 후 1~3초 쉬는 느낌: 다음 타겟을 바로 안 고르고 타이머만 쓰려면 복잡해지므로
                // 랜덤 지점으로 계속 이동 (기력 소모 없음).
                wanderTarget = MapBounds.RandomPoint(transform.position.z);
            }
            transform.position = MapBounds.Clamp(
                Vector3.MoveTowards(transform.position, wanderTarget.Value, moveSpeed * dt));
            ResolveSeparation(dt);
        }

        /// <summary>
        /// 놀기·걷기 중 기물에 올려 앉히기. 성공 시 머물기(기력 0까지 생산)가 새로 시작된다.
        /// 이미 다른 요괴가 앉아 있거나 엔딩기물 제한이면 false.
        /// </summary>
        public bool TrySitOnProp(PropSlot prop)
        {
            if (prop == null || Stats.Stage == GrowthStage.Neok) return false;
            if (Stats.State == ActionState.Fainted) return false;
            if (Stats.State == ActionState.Slumped) return false; // 주저앉기는 SettleSlumpedAfterDrag

            ClearWalkDestination();
            LeaveCurrentProp();
            isWandering = false;
            wanderTarget = null;

            if (!prop.CanBeUsedBy(this)) return false;
            if (!prop.TryOccupy(this)) return false;

            currentProp = prop;
            var p = prop.transform.position;
            transform.position = MapBounds.Clamp(new Vector3(p.x, p.y, transform.position.z));
            EnterStaying();
            Requests.NotifySatOnProp(prop);
            return true;
        }

        private void ClearWalkDestination()
        {
            if (destination != null)
            {
                destination.ReleaseReservation(this);
                destination = null;
            }
            isWandering = false;
            wanderTarget = null;
        }

        // ---------------- Slumped / Fainted ----------------

        private void EnterSlumped()
        {
            // 6-2 기물 배정 규칙: 주저앉기·기절 중에는 현재 기물을 계속 점유한다 (Vacate 호출 안 함)
            Stats.State = ActionState.Slumped;
            Stats.StateTimer = 0f;
            Requests.ClearAll(); // 신규 요구 없음·진행 중 요구도 정리
        }

        private void TickSlumped(float dt)
        {
            TickSlumpedSlice(dt);
        }

        private float TickSlumpedSlice(float dt)
        {
            float timeToFaint = Mathf.Max(0f, FaintThresholdSeconds - Stats.StateTimer);
            float slice = Mathf.Min(dt, timeToFaint > 0f ? timeToFaint : dt);
            if (slice <= 0f) slice = dt;

            Stats.StateTimer += slice;
            if (Stats.StateTimer >= FaintThresholdSeconds)
            {
                Stats.State = ActionState.Fainted;
                Stats.StateTimer = 0f;
                Requests.ClearAll(); // 기절 시 요구 삭제 [확정]
                ShowFaintedEllipsis();
            }
            return slice;
        }

        private float TickPlayingSlice(float dt)
        {
            float timeToEnd = Mathf.Max(0f, PlayDurationSeconds - Stats.StateTimer);
            float slice = Mathf.Min(dt, timeToEnd > 0f ? timeToEnd : dt);
            if (slice <= 0f) slice = dt;

            Stats.StateTimer += slice;
            if (Stats.StateTimer >= PlayDurationSeconds)
                EnterWalking();
            return slice;
        }
    }
}
