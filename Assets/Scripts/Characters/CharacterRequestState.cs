using UnityEngine;
using Yoegoe.Data;
using Yoegoe.Economy;
using Yoegoe.UI;

namespace Yoegoe.Characters
{
    /// <summary>10장 요구(공양물·기물) 상태. CharacterAgent가 소유.</summary>
    public class CharacterRequestState
    {
        public const float OfferingDurationSeconds = 60f;
        public const float OfferingCooldownSeconds = 5f * 60f;
        public const float PropDurationSeconds = 30f;

        static readonly int[] OfferingBoundaries = { 70, 60, 50, 40, 30, 20, 10 };

        public OfferingData OfferingRequest { get; private set; }
        public PropSlot PropRequest { get; private set; }
        public float OfferingExpireAt { get; private set; }
        public float PropExpireAt { get; private set; }
        public float CooldownUntil { get; private set; }

        public bool HasOfferingRequest => OfferingRequest != null;
        public bool HasPropRequest => PropRequest != null;

        SpriteRenderer offeringIcon;
        SpriteRenderer propIcon;
        TextMesh propLabel;
        readonly CharacterAgent owner;

        public CharacterRequestState(CharacterAgent agent) => owner = agent;

        public void Tick(float dt)
        {
            if (HasOfferingRequest && Time.time >= OfferingExpireAt)
                ClearOfferingRequest();
            if (HasPropRequest && Time.time >= PropExpireAt)
                ClearPropRequest();

            UpdateVisualPositions();
        }

        public void NotifyStaminaDrain(float before, float after)
        {
            if (!CanSpawnOfferingRequest()) return;
            if (HasOfferingRequest) return;
            if (Time.time < CooldownUntil) return;

            for (int i = 0; i < OfferingBoundaries.Length; i++)
            {
                int b = OfferingBoundaries[i];
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
            var st = owner.Stats.State;
            if (st == ActionState.Slumped || st == ActionState.Fainted) return false;
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

        /// <summary>머물기에서 놀기로 일어남 → 기물 요구(엔딩 제외 빈 기물).</summary>
        public void TryStartPropRequest()
        {
            if (owner == null || owner.Stats.Stage != GrowthStage.Hon) return;
            if (owner.Stats.State == ActionState.Slumped || owner.Stats.State == ActionState.Fainted) return;
            if (HasPropRequest) return;
            if (PropManager.Instance == null) return;

            PropSlot pick = null;
            var all = PropManager.Instance.All;
            var candidates = new System.Collections.Generic.List<PropSlot>();
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || !p.IsBuilt || p.IsOccupied || p.IsReserved) continue;
                if (p.data != null && p.data.isEndingProp) continue;
                if (!p.CanBeUsedBy(owner)) continue;
                candidates.Add(p);
            }
            if (candidates.Count == 0) return;
            pick = candidates[Random.Range(0, candidates.Count)];

            PropRequest = pick;
            PropExpireAt = Time.time + PropDurationSeconds;
            EnsurePropIcon();
        }

        public void ClearOfferingRequest()
        {
            OfferingRequest = null;
            if (offeringIcon != null) offeringIcon.gameObject.SetActive(false);
        }

        public void ClearPropRequest()
        {
            PropRequest = null;
            if (propIcon != null) propIcon.gameObject.SetActive(false);
            if (propLabel != null) propLabel.gameObject.SetActive(false);
        }

        public void ClearAll()
        {
            ClearOfferingRequest();
            ClearPropRequest();
        }

        /// <summary>상세에서 공양 급여. true면 처리 완료(호출측에서 인벤 차감·리프레시).</summary>
        public bool TryHandleFeed(OfferingData offering, bool isPurified, bool isPreferred,
            out int staminaGain, out float intimacyGain, out bool fulfilledRequest)
        {
            staminaGain = offering != null && offering.staminaGain > 0 ? offering.staminaGain : 20;
            intimacyGain = (!isPurified && isPreferred) ? 0.25f : 0f;
            fulfilledRequest = false;

            if (isPurified || !HasOfferingRequest)
                return false; // 일반 경로

            bool matches = offering != null && OfferingRequest != null
                && string.Equals(offering.offeringId, OfferingRequest.offeringId,
                    System.StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                staminaGain = 30;
                intimacyGain = 0.25f; // 선호 공양(요구 추가분 없음)
                fulfilledRequest = true;
                ClearOfferingRequest();
                CooldownUntil = Time.time + OfferingCooldownSeconds;
                // 고마워 → 꾸러미 판정 → 당첨 시에만 추가 대사 → 팝업
                bool gift = GiftBundle.RollAfterRequestFulfilled();
                if (gift)
                {
                    owner.ShowTempSpeechSequence(
                        new[] { "너무 맛있어. 고마워." },
                        () =>
                        {
                            owner.ShowTempSpeech("이거… 챙겨뒀어.");
                            if (GiftBundlePopup.Instance != null)
                                GiftBundlePopup.Instance.OpenForReward("선물꾸러미");
                        });
                }
                else
                {
                    owner.ShowTempSpeech("너무 맛있어. 고마워.");
                }
                return true;
            }

            // 다른 공양물 → 기력만 20, 요구 삭제(쿨다운 없음)
            staminaGain = 20;
            intimacyGain = 0f;
            ClearOfferingRequest();
            return true;
        }

        /// <summary>요구 기물에 앉히면 완료.</summary>
        public void NotifySatOnProp(PropSlot prop)
        {
            if (!HasPropRequest || prop == null || prop != PropRequest) return;
            ClearPropRequest();
            bool gift = GiftBundle.RollAfterRequestFulfilled();
            if (gift)
            {
                owner.ShowTempSpeechSequence(
                    new[] { "지금 하고 싶은 걸 어떻게 알았지? 고마워." },
                    () =>
                    {
                        owner.ShowTempSpeech("이거… 챙겨뒀어.");
                        if (GiftBundlePopup.Instance != null)
                            GiftBundlePopup.Instance.OpenForReward("선물꾸러미");
                    });
            }
            else
            {
                owner.ShowTempSpeech("지금 하고 싶은 걸 어떻게 알았지? 고마워.");
            }
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
            // 아이콘 없으면 작은 점
            if (offeringIcon.sprite == null)
            {
                offeringIcon.sprite = WhiteSprite();
                offeringIcon.color = new Color(1f, 0.85f, 0.4f, 0.95f);
                offeringIcon.transform.localScale = Vector3.one * 0.25f;
                offeringIcon.gameObject.SetActive(true);
            }
        }

        void EnsurePropIcon()
        {
            if (propIcon == null)
            {
                var go = new GameObject(owner.name + "_ReqProp");
                propIcon = go.AddComponent<SpriteRenderer>();
                propIcon.sortingOrder = 1210;
            }
            var sr = PropRequest != null ? PropRequest.GetComponentInChildren<SpriteRenderer>() : null;
            propIcon.sprite = sr != null ? sr.sprite : WhiteSprite();
            propIcon.color = new Color(1f, 1f, 1f, 0.92f);
            propIcon.transform.localScale = Vector3.one * 0.35f;
            propIcon.gameObject.SetActive(true);

            if (propLabel == null)
            {
                var go = new GameObject(owner.name + "_ReqPropLabel");
                propLabel = go.AddComponent<TextMesh>();
                propLabel.anchor = TextAnchor.MiddleCenter;
                propLabel.characterSize = 0.05f;
                propLabel.fontSize = 42;
                propLabel.color = new Color(1f, 0.95f, 0.8f);
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 1211;
            }
            propLabel.text = "?";
            propLabel.gameObject.SetActive(true);
        }

        void UpdateVisualPositions()
        {
            float top = 0.55f;
            var body = owner.GetComponentInChildren<SpriteRenderer>();
            if (body != null && body.sprite != null)
                top = body.bounds.extents.y + 0.35f;
            else
                top = 0.55f;

            Vector3 pos = owner.transform.position + Vector3.up * (top + 0.35f);
            if (offeringIcon != null && offeringIcon.gameObject.activeSelf)
                offeringIcon.transform.position = pos;
            if (propIcon != null && propIcon.gameObject.activeSelf)
                propIcon.transform.position = pos;
            if (propLabel != null && propLabel.gameObject.activeSelf)
                propLabel.transform.position = pos + Vector3.up * 0.35f;
        }

        void HideMonologueIfAny()
        {
            // CharacterAgent 혼잣말 숨김은 공개 API로
            owner.HideMonologueForRequest();
        }

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
            if (propIcon != null) Object.Destroy(propIcon.gameObject);
            if (propLabel != null) Object.Destroy(propLabel.gameObject);
            offeringIcon = null;
            propIcon = null;
            propLabel = null;
        }
    }
}
