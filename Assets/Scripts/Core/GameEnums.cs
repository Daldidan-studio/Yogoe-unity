namespace KSpirits.Core
{
    public enum YokaiStage
    {
        Spirit = 0, // 넋
        Apparition = 1, // 괴
        Manifest = 2 // 혼
    }

    public enum ScrollMode
    {
        Nurture,
        Training,
        Summon,
        Evolution
    }

    public enum EndingType
    {
        None,
        Ascension,
        Demonization,
        Escape,
        TutorialComplete,
        HiddenEndingZero
    }

    public enum TutorialStepId
    {
        FirstMeeting = 1,
        FirstOffering = 2,
        EvolveToApparition = 3,
        Petting = 4,
        Training = 5,
        ItemCollect = 6,
        EnergyWarning = 7,
        EvolveToManifest = 8,
        MemoryView = 9,
        BlackeningChoice = 10,
        BlackRabbitFlee = 11,
        ImugiRestore = 12,
        WishBranch = 13,
        CardComplete = 14,
        Done = 99
    }

    public enum ItemId
    {
        PurifiedWater = 0,
        HoneyRiceCake = 1,
        RedBeanRiceCake = 2,
        Incense = 3,
        Coin = 4
    }

    /// <summary>말풍선 배치 슬롯. Scene의 DialogueAnchors/{이름} 과 1:1.</summary>
    public enum DialogueLayoutId
    {
        BottomWide = 0,
        NearYokai = 1,
        AboveMortar = 2,
        TopNarration = 3
    }
}
