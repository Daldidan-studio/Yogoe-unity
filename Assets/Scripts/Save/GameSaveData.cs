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
        /// <summary>구세이브(필드 자체가 없던 시절)를 로드하면 1로 채워진다 — 마이그레이션 기준값.
        /// 새 세이브의 실제 버전 스탬프는 GameSaveBridge.CaptureFromWorld가 GameSaveMigration.CurrentVersion으로 찍는다.</summary>
        public int version = 1;

        /// <summary>저장 시각 (UTC DateTime.Ticks).</summary>
        public long savedAtUtcTicks;

        public EconomySave economy = new EconomySave();
        public PropSave[] props = Array.Empty<PropSave>();
        public AgentSave[] agents = Array.Empty<AgentSave>();

        /// <summary>진행 중이던 윷놀이 매치(있을 때만). null이면 매치 없음 — 앱을 껐다 켜거나
        /// 나갔다 들어와도 보드 상태(말 위치·완주 여부)를 그대로 이어간다.</summary>
        public YutMatchSave yutMatch;
    }

    [Serializable]
    public class YutMatchSave
    {
        public YutPieceSave[] playerPieces = Array.Empty<YutPieceSave>();
        public YutPieceSave opponentPiece = new YutPieceSave();

        /// <summary>이번 매치에서 뽑힌 특수 칸(엽전/공양물/보물상자) 배치 — 나갔다 들어오거나
        /// 앱을 껐다 켜도 말 위치는 그대로인데 특수 칸만 새로 섞이면 안 되므로 저장한다.
        /// nodeId/kind가 병렬 배열(같은 인덱스끼리 짝)인 이유는 JsonUtility가 Dictionary를
        /// 직렬화하지 못해서다.</summary>
        public int[] specialSquareNodeIds = Array.Empty<int>();
        public int[] specialSquareKinds = Array.Empty<int>();
        /// <summary>공양물 칸마다 무슨 공양물이 배정됐는지(offeringId) — 위와 같은 이유로 병렬 배열.</summary>
        public int[] specialOfferingNodeIds = Array.Empty<int>();
        public string[] specialOfferingIds = Array.Empty<string>();
    }

    [Serializable]
    public class YutPieceSave
    {
        public string id;
        public string displayName;
        /// <summary>-1이면 대기(보드 밖).</summary>
        public int nodeId = -1;
        public bool finished;
        public int[] history = Array.Empty<int>();
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
        /// <summary>다음 윷 토큰 충전 예정 UTC ticks (2·10장: 30분마다 1개). 0이면 구세이브/충전 대기 없음.</summary>
        public long yutTokenRegenNextUtcTicks;
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

        /// <summary>공양물 인벤 (정화수 제외). null이면 구세이브 — StartingState 유지.</summary>
        public OfferingCountSave[] offerings;
    }

    [Serializable]
    public class OfferingCountSave
    {
        public string offeringId;
        public int count;
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
