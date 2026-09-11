using UnityEngine;
using Yoegoe.Data;

namespace Yoegoe.Economy
{
    /// <summary>10장 선물꾸러미 판정. 유저 공용 카운터(요괴별 아님).</summary>
    public static class GiftBundle
    {
        public enum ContentKind { Offering, PurifiedWater, AdTicket }

        /// <summary>연속 빈손 횟수. 4면 다음은 확정.</summary>
        public static int MissStreak { get; private set; }

        /// <summary>맨 처음 들어준 요구는 확률 없이 확정.</summary>
        public static bool FirstGrantDone { get; private set; }

        public static int AdTickets { get; private set; }

        public static void ResetFromSave(int missStreak, bool firstGrantDone, int adTickets)
        {
            MissStreak = Mathf.Max(0, missStreak);
            FirstGrantDone = firstGrantDone;
            AdTickets = Mathf.Max(0, adTickets);
        }

        public static void CaptureToSave(out int missStreak, out bool firstGrantDone, out int adTickets)
        {
            missStreak = MissStreak;
            firstGrantDone = FirstGrantDone;
            adTickets = AdTickets;
        }

        public static bool TrySpendAdTicket(int amount = 1)
        {
            if (amount <= 0 || AdTickets < amount) return false;
            AdTickets -= amount;
            return true;
        }

        /// <summary>윷놀이 보물상자 등 다른 출처에서 광고보상권을 직접 지급할 때.</summary>
        public static void AddAdTickets(int amount)
        {
            if (amount <= 0) return;
            AdTickets += amount;
        }

        /// <summary>요구 들어주기 직후 호출. true면 꾸러미 지급.</summary>
        public static bool RollAfterRequestFulfilled()
        {
            if (!FirstGrantDone)
            {
                FirstGrantDone = true;
                MissStreak = 0;
                return true;
            }

            if (MissStreak >= 4)
            {
                MissStreak = 0;
                return true;
            }

            if (Random.value < 0.2f)
            {
                MissStreak = 0;
                return true;
            }

            MissStreak++;
            return false;
        }

        public static ContentKind RollContent()
        {
            float r = Random.value;
            if (r < 0.25f) return ContentKind.Offering;
            if (r < 0.75f) return ContentKind.PurifiedWater;
            return ContentKind.AdTicket;
        }

        /// <summary>
        /// 꾸러미 내용 지급. specificOffering이 있으면 공양물 종류를 고정(광고 하나 더용).
        /// grantedOffering은 Offering일 때 실제 지급된 공양물(폴백 시 null).
        /// </summary>
        public static void Grant(
            ContentKind kind,
            OfferingData[] catalog,
            out string displayName,
            out Sprite icon,
            out OfferingData grantedOffering,
            OfferingData specificOffering = null)
        {
            displayName = "";
            icon = null;
            grantedOffering = null;
            switch (kind)
            {
                case ContentKind.PurifiedWater:
                    GameEconomy.Instance.AddPurifiedWater(1);
                    displayName = "정화수 x1";
                    if (catalog != null)
                    {
                        foreach (var o in catalog)
                        {
                            if (o != null && o.kind == OfferingKind.PurifiedWater)
                            {
                                icon = o.icon;
                                break;
                            }
                        }
                    }
                    break;
                case ContentKind.AdTicket:
                    AdTickets++;
                    displayName = "광고 보상권 x1";
                    break;
                default:
                    var pick = specificOffering != null && specificOffering.kind != OfferingKind.PurifiedWater
                        ? specificOffering
                        : PickRandomOffering(catalog);
                    if (pick != null)
                    {
                        GameEconomy.Instance.AddOffering(pick, 1);
                        displayName = (string.IsNullOrEmpty(pick.displayName) ? pick.offeringId : pick.displayName) + " x1";
                        icon = pick.icon;
                        grantedOffering = pick;
                    }
                    else
                    {
                        GameEconomy.Instance.AddPurifiedWater(1);
                        displayName = "정화수 x1";
                    }
                    break;
            }
        }

        static OfferingData PickRandomOffering(OfferingData[] catalog)
        {
            if (catalog == null || catalog.Length == 0) return null;
            int guard = 0;
            while (guard++ < 24)
            {
                var o = catalog[Random.Range(0, catalog.Length)];
                if (o == null) continue;
                if (o.kind == OfferingKind.PurifiedWater) continue;
                return o;
            }
            return null;
        }
    }
}
