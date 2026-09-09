using System;
using Yoegoe.Data;

namespace Yoegoe.Save
{
    /// <summary>
    /// 디스크/PlayerPrefs에 넣는 세이브 DTO만 둔다. 씬·Agent를 직접 참조하지 않는다.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public int version = 1;

        /// <summary>저장 시각 (UTC DateTime.Ticks).</summary>
        public long savedAtUtcTicks;

        public EconomySave economy = new EconomySave();
        public PropSave[] props = Array.Empty<PropSave>();
        public AgentSave[] agents = Array.Empty<AgentSave>();
    }

    [Serializable]
    public class EconomySave
    {
        public BigNumberSave merit = new BigNumberSave();
        /// <summary>앱 재시작 일괄 수거 미수령분.</summary>
        public BigNumberSave pendingBatchMerit = new BigNumberSave();
        public int yeopjeon;
        public int hyang;
        public int purifiedWater;
        public int yutToken;
        public int yutTokenMax = 5;
        /// <summary>플레이어가 구매한 기물 수 (prebuilt 제외). 다음 구매 비용 n = 이 값+1.</summary>
        public int propsPurchasedCount;
        /// <summary>선물꾸러미 연속 빈손 / 첫 확정 / 광고보상권.</summary>
        public int giftMissStreak;
        public bool giftFirstGrantDone;
        public int adRewardTickets;

        /// <summary>12장 고가구점: 좌·우 공양 id, 다음 자동 갱신 UTC ticks.</summary>
        public string shopLeftOfferingId = "";
        public string shopRightOfferingId = "";
        public long shopNextRefreshUtcTicks;
    }

    [Serializable]
    public class PropSave
    {
        public string propId;
        public int level = 1;
        /// <summary>건립 여부. 구세이브에 필드 없으면 true로 취급(기본값).</summary>
        public bool isBuilt = true;
        public BigNumberSave pendingMerit = new BigNumberSave();
        /// <summary>오프라인 생산 계산용 (저장 시점 스냅샷).</summary>
        public double baseProductionPerMinute = 100;
        public bool isEndingProp;
        public string ownerCharacterId;
    }

    [Serializable]
    public class AgentSave
    {
        public string characterId; // CharacterData.id 또는 displayName
        public GrowthStage stage;
        public float intimacy;
        public float stamina;
        public ActionState state;
        public float stateTimer;
        public float posX;
        public float posY;
        /// <summary>앉아/점유 중인 기물 propId. 없으면 빈 문자열.</summary>
        public string occupiedPropId;
    }

    [Serializable]
    public class BigNumberSave
    {
        public double mantissa;
        public int exponent;

        public static BigNumberSave From(Yoegoe.Core.BigNumber n) =>
            new BigNumberSave { mantissa = n.Mantissa, exponent = n.Exponent };

        public Yoegoe.Core.BigNumber ToBigNumber() =>
            new Yoegoe.Core.BigNumber(mantissa, exponent);
    }
}
