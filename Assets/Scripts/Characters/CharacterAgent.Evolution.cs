using System.Collections;
using UnityEngine;
using Yoegoe.Core;
using Yoegoe.Data;

namespace Yoegoe.Characters
{
    // 넋(도깨비불) 부유 + 넋→혼 진화 연출.
    // 넋 단계는 CharacterAgent의 걷기/머물기 상태머신을 타지 않고 이 부유 로직만 쓴다
    // (9장: 소환된 넋은 화면에 뜨는 고정 도깨비불로 취급하고 정화수로만 기력을 채운다).
    public partial class CharacterAgent
    {
        private const float NeokDriftSpeed = 0.7f;
        private const float NeokBobAmplitude = 0.14f;
        private const float NeokBobSpeed = 2.4f;

        private Vector3 neokLogicalPos;
        private Vector3? neokDriftTarget;
        private float neokBobPhase;
        private bool evolvingToHon;

        /// <summary>
        /// 진화 연출 종료 후 확인 창을 요청한다 (message, onConfirmed).
        /// UI(EvolutionConfirmPopup)가 구독해서 실제 팝업을 띄운다 — CharacterAgent는 UI를 모른다.
        /// 구독자가 없으면 EvolveToHonFxRoutine이 짧은 대기 후 자동 진행한다.
        /// </summary>
        public static event System.Action<string, System.Action> EvolutionConfirmRequested;

        /// <summary>소환 직후 넋 상태로 고정 (Start보다 먼저 호출).</summary>
        public void ApplyFreshNeokSummon()
        {
            statsAppliedExternally = true;
            Stats.Stage = GrowthStage.Neok;
            Stats.Intimacy = 0f;
            Stats.Stamina = 0f;
            Stats.State = ActionState.Walking;
            Stats.StateTimer = 0f;
            neokLogicalPos = transform.position;
            neokDriftTarget = null;
            neokBobPhase = Random.Range(0f, Mathf.PI * 2f);
            lastPosition = transform.position;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ApplyAnimationFrameImmediate();
        }

        /// <summary>넋 → 혼. 친밀도 0부터. playFx면 줌인·변화 후 확인 창.</summary>
        public void EvolveToHon(bool playFx = true)
        {
            if (Stats.Stage != GrowthStage.Neok || evolvingToHon) return;

            Stats.Stage = GrowthStage.Hon;
            Stats.Intimacy = 0f;
            Stats.Stamina = Mathf.Max(Stats.Stamina, 100f);
            Stats.StateTimer = 0f;
            Requests.ClearAll();
            transform.position = MapBounds.Clamp(neokLogicalPos.sqrMagnitude > 0.0001f
                ? neokLogicalPos
                : transform.position);
            lastPosition = transform.position;

            if (playFx && isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                StartCoroutine(EvolveToHonFxRoutine());
            }
            else
            {
                CharacterSpawner.EnsureHonVisual(this);
                EnterWalking();
                ApplyAnimationFrameImmediate();
            }

            Debug.Log($"[CharacterAgent] {Data?.displayName ?? name} 넋→혼 진화");
        }

        private IEnumerator EvolveToHonFxRoutine()
        {
            evolvingToHon = true;
            CeremonyGate.Begin();

            var focus = Yoegoe.Debugging.MapCameraFocus.Instance;
            if (focus != null)
            {
                var cam = Camera.main;
                float ortho = cam != null ? Mathf.Max(2.2f, cam.orthographicSize * 0.55f) : 2.8f;
                focus.Focus(transform.position, ortho, 0.55f);
                yield return new WaitForSecondsRealtime(0.55f);
            }

            var meshRenderer = GetComponent<MeshRenderer>();
            Vector3 neokScale = transform.localScale;
            const float fadeOut = 0.45f;
            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fadeOut);
                float e = u * u;
                transform.localScale = Vector3.Lerp(neokScale, Vector3.zero, e);
                SetMeshAlpha(meshRenderer, 1f - u);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            CharacterSpawner.EnsureHonVisual(this);

            var scaleSettings = ArtScaleSettings.GetOrDefault();
            Vector3 honScale = Vector3.one * scaleSettings.characterScale;
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = 0f;
                spriteRenderer.color = c;
            }
            transform.localScale = honScale * 0.7f;

            const float fadeIn = 0.65f;
            t = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeIn));
                transform.localScale = Vector3.Lerp(honScale * 0.7f, honScale, u);
                if (spriteRenderer != null)
                {
                    var c = spriteRenderer.color;
                    c.a = u;
                    spriteRenderer.color = c;
                }
                UpdateSortingOrder();
                yield return null;
            }

            transform.localScale = honScale;
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = 1f;
                spriteRenderer.color = c;
            }

            ApplyAnimationFrameImmediate();

            string who = Data != null && !string.IsNullOrEmpty(Data.displayName)
                ? Data.displayName
                : name;
            string msg = who + "가 혼으로 진화했다.";

            bool confirmed = false;
            if (EvolutionConfirmRequested != null)
            {
                EvolutionConfirmRequested.Invoke(msg, () => confirmed = true);
                while (!confirmed) yield return null;
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.8f);
            }

            if (focus != null) focus.Restore(0.45f);
            yield return new WaitForSecondsRealtime(0.2f);

            EnterWalking();
            evolvingToHon = false;
            CeremonyGate.End();
            Yoegoe.Save.GameSaveBridge.SaveFromWorld();
        }

        private static void SetMeshAlpha(MeshRenderer meshRenderer, float alpha)
        {
            if (meshRenderer == null) return;
            var mat = meshRenderer.material;
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor"))
            {
                var c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            if (mat.HasProperty("_Color"))
            {
                var c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
        }

        /// <summary>넋 도깨비불: 맵을 천천히 떠돌며 위아래로 둥둥.</summary>
        private void TickNeokFloat(float dt)
        {
            if (neokLogicalPos.sqrMagnitude < 0.0001f && transform.position.sqrMagnitude > 0.0001f)
                neokLogicalPos = transform.position;

            if (!neokDriftTarget.HasValue
                || Vector2.Distance(neokLogicalPos, neokDriftTarget.Value) < 0.12f)
            {
                PickNeokDriftTarget();
            }

            if (neokDriftTarget.HasValue)
            {
                neokLogicalPos = Vector3.MoveTowards(
                    neokLogicalPos, neokDriftTarget.Value, NeokDriftSpeed * dt);
                neokLogicalPos = MapBounds.Clamp(neokLogicalPos);
            }

            neokBobPhase += dt * NeokBobSpeed;
            float bob = Mathf.Sin(neokBobPhase) * NeokBobAmplitude;
            transform.position = new Vector3(neokLogicalPos.x, neokLogicalPos.y + bob, neokLogicalPos.z);
            lastPosition = transform.position;
        }

        private void PickNeokDriftTarget()
        {
            Vector2 min = MapBounds.Min;
            Vector2 max = MapBounds.Max;
            // bounds 미설정 시 현재 근처만
            if (max.x - min.x < 0.5f || max.y - min.y < 0.5f)
            {
                neokDriftTarget = neokLogicalPos + (Vector3)(Random.insideUnitCircle * 1.2f);
                return;
            }

            neokDriftTarget = new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                neokLogicalPos.z);
        }
    }
}
