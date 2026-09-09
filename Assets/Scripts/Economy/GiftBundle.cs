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

        /// <summary>광고 시청(스텁) 또는 광고보상권으로 추가 1회 지급용. 내용만 다시 굴림.</summary>
        public static void GrantBonusRoll(OfferingData[] catalog, out string displayName, out Sprite icon)
        {
            Grant(RollContent(), catalog, out displayName, out icon);
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

        public static void Grant(ContentKind kind, OfferingData[] catalog, out string displayName, out Sprite icon)
        {
            displayName = "";
            icon = null;
            switch (kind)
            {
                case ContentKind.PurifiedWater:
                    GameEconomy.AddPurifiedWater(1);
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
                    var pick = PickRandomOffering(catalog);
                    if (pick != null)
                    {
                        GameEconomy.AddOffering(pick, 1);
                        displayName = (string.IsNullOrEmpty(pick.displayName) ? pick.offeringId : pick.displayName) + " x1";
                        icon = pick.icon;
                    }
                    else
                    {
                        GameEconomy.AddPurifiedWater(1);
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
