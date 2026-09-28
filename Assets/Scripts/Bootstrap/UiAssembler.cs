using UnityEngine;
using Yoegoe.Data;
using Yoegoe.UI;

namespace Yoegoe.Bootstrap
{
    /// <summary>
    /// Main 씬 UI 배선: offerings 카탈로그, HUD·팝업·오버레이 화면.
    /// Inspector 필드는 <see cref="Main"/>에 두고 여기로 넘긴다.
    /// </summary>
    public static class UiAssembler
    {
        public struct Config
        {
            public Font hudFont;
            public Sprite purifiedWaterIcon;
            public OfferingData[] offerings;
            public CharacterData goraniData;
        }

        /// <summary>
        /// Inspector에 offerings가 비어 있으면 StartingStateSettings 목록을 쓰고,
        /// 공양간 레시피 결과물(음식·공양물)을 합쳐 전체 카탈로그로 만든다.
        /// </summary>
        public static OfferingData[] EnsureOfferingsCatalog(OfferingData[] offerings)
        {
            if (offerings == null || offerings.Length == 0)
                offerings = StartingStateSettings.Get()?.startingOfferings;
            return OfferingCatalog.Build(offerings);
        }

        public static void WireHud(Config cfg)
        {
            var detail = Object.FindAnyObjectByType<DetailScreen>(FindObjectsInactive.Include);
            if (detail == null)
            {
                var detailGO = new GameObject("DetailScreen");
                detailGO.SetActive(false);
                detail = detailGO.AddComponent<DetailScreen>();
                detailGO.SetActive(true);
            }
            detail.font = cfg.hudFont;
            detail.offerings = cfg.offerings;
            detail.purifiedWaterIcon = cfg.purifiedWaterIcon;
            if (!detail.gameObject.activeSelf)
                detail.gameObject.SetActive(true);

            var purchaseGO = new GameObject("PropPurchasePopup");
            purchaseGO.SetActive(false);
            var purchase = purchaseGO.AddComponent<PropPurchasePopup>();
            purchase.font = cfg.hudFont;
            purchaseGO.SetActive(true);

            var summonGO = new GameObject("SummonPopup");
            summonGO.SetActive(false);
            var summon = summonGO.AddComponent<SummonPopup>();
            summon.font = cfg.hudFont;
            summon.goraniData = cfg.goraniData;
            summonGO.SetActive(true);

            var ceremonyGO = new GameObject("SummonCeremony");
            var ceremony = ceremonyGO.AddComponent<SummonCeremony>();
            ceremony.font = cfg.hudFont;
            ceremony.goraniData = cfg.goraniData;

            var shop = Object.FindAnyObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (shop == null)
            {
                Debug.LogError(
                    "[UiAssembler] ShopScreen 프리팹 인스턴스가 씬에 없습니다. Main 씬에 Prefab 인스턴스를 배치하세요.");
            }
            else
            {
                shop.font = cfg.hudFont;
                shop.offerings = cfg.offerings;
                shop.shopBackground = Resources.Load<Sprite>("UI/ShopInterior");
                shop.imugiSprite = Resources.Load<Sprite>("UI/ImugiPortrait");
                if (!shop.gameObject.activeSelf)
                    shop.gameObject.SetActive(true);
            }

            var gongyanggan = GongyangganScreen.Resolve();
            if (gongyanggan == null)
            {
                Debug.LogError(
                    "[UiAssembler] GongyangganScreen 프리팹이 없습니다. Main 씬에 Prefab 인스턴스를 배치하세요.");
            }
            else
            {
                gongyanggan.font = cfg.hudFont;
            }

            var yut = Object.FindAnyObjectByType<YutScreen>(FindObjectsInactive.Include);
            if (yut == null)
            {
                var yutGO = new GameObject("YutScreen");
                yutGO.SetActive(false);
                yut = yutGO.AddComponent<YutScreen>();
                yutGO.SetActive(true);
            }
            yut.font = cfg.hudFont;
            if (!yut.gameObject.activeSelf)
                yut.gameObject.SetActive(true);

            var giftGO = new GameObject("GiftBundlePopup");
            giftGO.SetActive(false);
            var gift = giftGO.AddComponent<GiftBundlePopup>();
            gift.font = cfg.hudFont;
            gift.offerings = cfg.offerings;
            giftGO.SetActive(true);

            var batchGO = new GameObject("BatchCollectPopup");
            batchGO.SetActive(false);
            var batch = batchGO.AddComponent<BatchCollectPopup>();
            batch.font = cfg.hudFont;
            batchGO.SetActive(true);

            var yutShopGO = new GameObject("YutTokenShopPopup");
            yutShopGO.SetActive(false);
            var yutShop = yutShopGO.AddComponent<YutTokenShopPopup>();
            yutShop.font = cfg.hudFont;
            yutShopGO.SetActive(true);

            var hud = Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            if (hud == null)
            {
                var hudGO = new GameObject("Hud");
                hudGO.SetActive(false);
                hud = hudGO.AddComponent<GameHud>();
                hudGO.SetActive(true);
            }

            hud.font = cfg.hudFont;
            hud.purifiedWaterIcon = cfg.purifiedWaterIcon;
            hud.detailScreen = detail;
            if (!hud.gameObject.activeSelf)
                hud.gameObject.SetActive(true);
        }

        public static void ForceCloseOverlayScreens()
        {
            var detail = Object.FindAnyObjectByType<DetailScreen>(FindObjectsInactive.Include);
            if (detail != null) detail.Close();

            var shop = Object.FindAnyObjectByType<ShopScreen>(FindObjectsInactive.Include);
            if (shop != null) shop.Close();

            var gongyanggan = Object.FindAnyObjectByType<GongyangganScreen>(FindObjectsInactive.Include);
            if (gongyanggan != null) gongyanggan.Close();

            var yut = Object.FindAnyObjectByType<YutScreen>(FindObjectsInactive.Include);
            if (yut != null) yut.Close();

            var attendance = Object.FindAnyObjectByType<AttendanceScreen>(FindObjectsInactive.Include);
            if (attendance != null) attendance.Close();
        }
    }
}
