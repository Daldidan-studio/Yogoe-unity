using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    /// <summary>10장 공양물 요구 상태. CharacterAgent가 소유. (기물 요구는 최종 밸런스에서 삭제)</summary>
    public class CharacterRequestState
    {
        public const float OfferingDurationSeconds = 60f;
        public const float OfferingCooldownSeconds = 5f * 60f;

        /// <summary>
        /// 요구 완료 보상으로 선물꾸러미 당첨. UI(GiftBundlePopup)가 구독해서 팝업을 연다.
        /// </summary>
        public static event System.Action<string> GiftBundleAwarded;

        public OfferingData OfferingRequest { get; private set; }
        public float OfferingExpireAt { get; private set; }
        public float CooldownUntil { get; private set; }

        public bool HasOfferingRequest => OfferingRequest != null;

        SpriteRenderer offeringIcon;
        readonly CharacterAgent owner;

        public CharacterRequestState(CharacterAgent agent) => owner = agent;

        public void Tick(float dt)
        {
            if (HasOfferingRequest && Time.time >= OfferingExpireAt)
                ClearOfferingRequest();
            UpdateVisualPositions();
        }

        /// <summary>
        /// 기력 소모 시: 현재 기력이 (최대−20) 이하인 구간에서 −5 절대경계를 하향 통과하면 요구.
        /// </summary>
        public void NotifyStaminaDrain(float before, float after)
        {
            if (!CanSpawnOfferingRequest()) return;
            if (HasOfferingRequest) return;
            if (Time.time < CooldownUntil) return;

            float max = owner.MaxStamina;
            float regionCeiling = max - 20f;
            if (regionCeiling < 5f) return;

            int top = Mathf.FloorToInt(regionCeiling / 5f) * 5;
            for (int b = top; b >= 5; b -= 5)
            {
                if (before > b && after <= b)
                {
                    TryStartOfferingRequest();
                    return;
                }
            }
        }

        public bool CanSpawnOfferingRequest()
        {
            if (owner == null || owner.Stats == null) return false;
            if (owner.Stats.Stage != GrowthStage.Hon) return false;
            // 기절만 제외. 놀기(기력0 포함)·걷기·머물기는 가능.
            if (owner.Stats.State == ActionState.Fainted) return false;
            return true;
        }

        public void TryStartOfferingRequest()
        {
            if (!CanSpawnOfferingRequest() || HasOfferingRequest) return;
            if (Time.time < CooldownUntil) return;

            var offering = PickPreferredOffering();
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

        /// <summary>상세에서 공양 급여. true면 처리 완료(호출측에서 인벤 차감·리프레시).</summary>
        public bool TryHandleFeed(OfferingData offering, bool isPurified, bool isPreferred,
            out int staminaGain, out float intimacyGain, out bool fulfilledRequest)
        {
            staminaGain = offering != null && offering.staminaGain > 0 ? offering.staminaGain : 20;
            intimacyGain = (!isPurified && isPreferred) ? 0.25f : 0f;
            fulfilledRequest = false;

            if (isPurified || !HasOfferingRequest)
                return false;

            bool matches = offering != null && OfferingRequest != null
                && string.Equals(offering.offeringId, OfferingRequest.offeringId,
                    System.StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                staminaGain = 30;
                intimacyGain = 0.25f;
                fulfilledRequest = true;
                ClearOfferingRequest();
                CooldownUntil = Time.time + OfferingCooldownSeconds;
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

            staminaGain = 20;
            intimacyGain = 0f;
            ClearOfferingRequest();
            return true;
        }

        OfferingData PickPreferredOffering()
        {
            if (owner.Data == null) return null;
            var prefs = owner.Data.preferredOfferings;
            if (prefs != null && prefs.Length > 0)
            {
                var list = new System.Collections.Generic.List<OfferingData>();
                foreach (var o in prefs)
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
