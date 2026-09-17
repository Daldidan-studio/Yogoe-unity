using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    /// <summary>10장 음식 요구. CharacterAgent가 소유. (기물 요구 삭제)</summary>
    public class CharacterRequestState
    {
        public const float OfferingDurationSeconds = 60f;
        public const float RequestIntervalMin = 3f * 60f;
        public const float RequestIntervalMax = 5f * 60f;
        /// <summary>기력 ≤ 최대 − 이 값 이면 요구 후보 구간.</summary>
        public const float StaminaRequestMargin = 25f;

        public static event System.Action<string> GiftBundleAwarded;

        public OfferingData OfferingRequest { get; private set; }
        public float OfferingExpireAt { get; private set; }
        public float NextRequestCheckAt { get; private set; }

        public bool HasOfferingRequest => OfferingRequest != null;

        SpriteRenderer offeringIcon;
        readonly CharacterAgent owner;

        public CharacterRequestState(CharacterAgent agent)
        {
            owner = agent;
            ScheduleNextCheck();
        }

        public void Tick(float dt)
        {
            if (HasOfferingRequest && Time.time >= OfferingExpireAt)
                ClearOfferingRequest();

            if (!HasOfferingRequest
                && CanSpawnOfferingRequest()
                && InLowStaminaBand()
                && Time.time >= NextRequestCheckAt)
            {
                TryStartOfferingRequest();
                ScheduleNextCheck();
            }

            UpdateVisualPositions();
        }

        /// <summary>레거시 훅. 주기 타이머로 대체됨.</summary>
        public void NotifyStaminaDrain(float before, float after) { }

        void ScheduleNextCheck()
        {
            NextRequestCheckAt = Time.time + Random.Range(RequestIntervalMin, RequestIntervalMax);
        }

        bool InLowStaminaBand()
        {
            if (owner == null) return false;
            return owner.Stats.Stamina <= owner.MaxStamina - StaminaRequestMargin + 0.001f;
        }

        public bool CanSpawnOfferingRequest()
        {
            if (owner == null || owner.Stats == null) return false;
            if (owner.Stats.Stage != GrowthStage.Hon) return false;
            if (owner.Stats.State == ActionState.Fainted) return false;
            return true;
        }

        public void TryStartOfferingRequest()
        {
            if (!CanSpawnOfferingRequest() || HasOfferingRequest) return;
            if (!InLowStaminaBand()) return;

            var offering = PickFoodRequest();
            if (offering == null) return;

            OfferingRequest = offering;
            OfferingExpireAt = Time.time + OfferingDurationSeconds;
            EnsureOfferingIcon();
            HideMonologueIfAny();
        }

        public void ClearOfferingRequest()
        {
            OfferingRequest = null;
            if (offeringIcon != null) offeringIcon.gameObject.SetActive(false);
        }

        public void ClearAll() => ClearOfferingRequest();

        /// <summary>상세에서 급여. true면 처리 완료(호출측에서 인벤 차감·리프레시).</summary>
        public bool TryHandleFeed(OfferingData offering, bool isPurified, bool isPreferred,
            out int staminaGain, out float intimacyGain, out bool fulfilledRequest)
        {
            staminaGain = offering != null ? offering.ResolveStaminaGain(isPreferred) : 3;
            intimacyGain = offering != null ? offering.ResolveIntimacyGain(isPreferred) : 0f;
            fulfilledRequest = false;

            if (isPurified || !HasOfferingRequest)
                return false;

            bool matches = offering != null && OfferingRequest != null
                && string.Equals(offering.offeringId, OfferingRequest.offeringId,
                    System.StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                // 음식 요구 완료: +12 · 친밀도 없음
                staminaGain = 12;
                intimacyGain = 0f;
                fulfilledRequest = true;
                ClearOfferingRequest();
                ScheduleNextCheck();
                bool gift = GiftBundle.RollAfterRequestFulfilled();
                if (gift)
                {
                    owner.ShowTempSpeechSequence(
                        new[] { "너무 맛있어. 고마워." },
                        () =>
                        {
                            owner.ShowTempSpeech("이거… 챙겨뒀어.");
                            GiftBundleAwarded?.Invoke("선물꾸러미");
                        });
                }
                else
                {
                    owner.ShowTempSpeech("너무 맛있어. 고마워.");
                }
                return true;
            }

            // 다른 것 주면 기력(일반 효과)만 오르고 요구 삭제
            ClearOfferingRequest();
            ScheduleNextCheck();
            return true;
        }

        OfferingData PickFoodRequest()
        {
            var ownedFood = new System.Collections.Generic.List<OfferingData>();
            var ownedAny = new System.Collections.Generic.List<OfferingData>();
            var eco = GameEconomy.Instance;
            if (eco != null)
            {
                var snap = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
                eco.CaptureOfferingCounts(snap);
                for (int i = 0; i < snap.Count; i++)
                {
                    var o = CharacterCatalog.FindOffering(snap[i].Key);
                    if (o == null || o.kind == OfferingKind.PurifiedWater) continue;
                    ownedAny.Add(o);
                    if (o.kind == OfferingKind.Food) ownedFood.Add(o);
                }
            }
            if (ownedFood.Count > 0) return ownedFood[Random.Range(0, ownedFood.Count)];
            if (ownedAny.Count > 0) return ownedAny[Random.Range(0, ownedAny.Count)];

            if (owner?.Data == null) return null;

            if (owner.Data.preferredOfferings != null && owner.Data.preferredOfferings.Length > 0)
            {
                var list = new System.Collections.Generic.List<OfferingData>();
                foreach (var o in owner.Data.preferredOfferings)
                    if (o != null && o.kind != OfferingKind.PurifiedWater) list.Add(o);
                if (list.Count > 0) return list[Random.Range(0, list.Count)];
            }

            if (CharacterCatalog.TryGet(owner.Data.id, out var entry) && entry?.preferredOfferings != null)
            {
                var list = new System.Collections.Generic.List<OfferingData>();
                foreach (var p in entry.preferredOfferings)
                {
                    if (p == null || string.IsNullOrEmpty(p.id)) continue;
                    var found = CharacterCatalog.FindOffering(p.id);
                    if (found != null) list.Add(found);
                }
                if (list.Count > 0) return list[Random.Range(0, list.Count)];
            }
            return null;
        }

        void EnsureOfferingIcon()
        {
            if (offeringIcon == null)
            {
                var go = new GameObject(owner.name + "_ReqOffering");
                offeringIcon = go.AddComponent<SpriteRenderer>();
                offeringIcon.sortingOrder = 1210;
            }
            offeringIcon.sprite = OfferingRequest != null ? OfferingRequest.icon : null;
            offeringIcon.color = Color.white;
            offeringIcon.transform.localScale = Vector3.one * 0.45f;
            offeringIcon.gameObject.SetActive(offeringIcon.sprite != null);
            if (offeringIcon.sprite == null)
            {
                offeringIcon.sprite = WhiteSprite();
                offeringIcon.color = new Color(1f, 0.85f, 0.4f, 0.95f);
                offeringIcon.transform.localScale = Vector3.one * 0.25f;
                offeringIcon.gameObject.SetActive(true);
            }
        }

        void UpdateVisualPositions()
        {
            if (offeringIcon == null || !offeringIcon.gameObject.activeSelf) return;

            float top = 0.55f;
            var body = owner.GetComponentInChildren<SpriteRenderer>();
            if (body != null && body.sprite != null)
                top = body.bounds.extents.y + 0.35f;

            offeringIcon.transform.position = owner.transform.position + Vector3.up * (top + 0.35f);
        }

        void HideMonologueIfAny() => owner.HideMonologueForRequest();

        static Sprite s_white;
        static Sprite WhiteSprite()
        {
            if (s_white != null) return s_white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            s_white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return s_white;
        }

        public void DestroyVisuals()
        {
            if (offeringIcon != null) Object.Destroy(offeringIcon.gameObject);
            offeringIcon = null;
        }
    }
}
