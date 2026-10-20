using System;
using System.Collections.Generic;
using Code.MapCreateSystem;
using Code.Units;

namespace Code.Persistence
{
    [Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtUtc;
        public int completedDay;
        public int gold;
        public int debt;
        public float buildDiscountRate;
        public int morale;
        public SavedPolicyState policies = new();
        public List<string> artifacts = new();
        public int merchantPurchaseCount;
        public List<SavedCount> villageConquests = new();
        public List<SavedNode> nodes = new();
        public List<SavedEdge> edges = new();
        public SavedRoster roster = new();
    }

    /// <summary>
    /// 확장 후보 자리에 이미 뽑아 둔 크기·특수 효과. 처음 방들은 중형으로 고정해 뽑으므로, 시드만으로는
    /// 불러온 뒤 같은 후보를 다시 만들 수 없다.
    /// </summary>
    [Serializable]
    public sealed class SavedCandidate
    {
        public int x;
        public int y;
        public int roomSize;
        public int roomFeature;

        // 후보에 보여 준 방 모양(SavedNode와 같은 형식).
        public string layoutId;
        public List<string> layoutRows = new();
        public int layoutDoors;
    }

    [Serializable]
    public sealed class SavedNode
    {
        public DungeonNodeType type;
        public int x;
        public int y;
        public int danger;

        // 공사 중이던 방. 옛 저장에는 없어서 0(완공)으로 읽힌다.
        public float constructionRemaining;
        public int constructionPaidGold;

        // 방 크기(RoomSize). 옛 저장에는 없어서 0(중형)으로 읽힌다.
        public int roomSize;

        // 방 특수 효과(RoomFeature). 옛 저장에는 없어서 0(없음)으로 읽힌다.
        public int roomFeature;

        // 방 모양: 특별 방 프리팹 이름(장식용) 또는 생성 표식, 칸 줄(위 줄부터 '.'/'#'), 문(RoomDoors).
        // 생성한 방은 다시 깎지 않고 이 줄로 되살린다.
        public string layoutId;
        public List<string> layoutRows = new();
        public int layoutDoors;

        // 모험가 사이의 소문(RoomRumor). 옛 저장에는 없어서 0(소문 없음)으로 읽힌다.
        public int rumorTraps;
        public int rumorDowns;
        public int rumorRetreats;

        public SavedBuilding centralBuilding;
        public List<SavedBuilding> cellBuildings = new();
        public List<SavedUnit> units = new();
    }

    [Serializable]
    public sealed class SavedEdge
    {
        public int fromX;
        public int fromY;
        public int toX;
        public int toY;
        public SavedBuilding building;
    }

    [Serializable]
    public sealed class SavedBuilding
    {
        public string assetKey;
        public int column = -1;
        public int row = -1;
        public int durability;
        public int storedGold;

        /// <summary>옛 저장의 마모. 지금은 내구도에 바로 깎이므로 새로 쓰지 않는다.</summary>
        public int wear;
        public bool closed;

        /// <summary>남은 수리 시간(게임 초). 0이면 수리 중이 아니다.</summary>
        public float repairRemaining;

        /// <summary>누적 수리 횟수. 다음 수리비 할증의 기준이다.</summary>
        public int repairCount;

        /// <summary>다시 열리기로 예약된 날. 0이면 닫힌 채로 예약하지 않았다.</summary>
        public int reopenOnDay;
    }

    [Serializable]
    public sealed class SavedUnit
    {
        public string assetKey;
        public bool isMainUnit;
        public int column;
        public int row;
        public UnitConditionState condition;
    }

    [Serializable]
    public sealed class SavedRoster
    {
        public List<SavedRosterUnit> availableUnits = new();
        public List<SavedCount> unitCandidates = new();
        public List<SavedCount> deployedUnits = new();
        public List<SavedCount> buildings = new();
        public List<string> unlockedUnits = new();
        public List<string> unlockedBuildings = new();
        public List<SavedApplicantGroup> applicants = new();
    }

    [Serializable]
    public sealed class SavedRosterUnit
    {
        public string assetKey;
        public UnitConditionState condition;
    }

    [Serializable]
    public sealed class SavedCount
    {
        public string assetKey;
        public int count;
    }

    [Serializable]
    public sealed class SavedApplicantGroup
    {
        public string assetKey;
        public List<SavedApplicant> applicants = new();
    }

    [Serializable]
    public sealed class SavedApplicant
    {
        public UnitConditionState condition;
        public int daysLeft;

        /// <summary>정가에 곱하는 시세. 옛 저장은 0으로 읽혀 정가로 본다.</summary>
        public float priceMultiplier;
    }

    [Serializable]
    public sealed class SavedPolicyState
    {
        public List<string> selected = new();
        public List<SavedActivePolicy> active = new();

        /// <summary>사건 기록을 담은 저장인가. 옛 저장은 false로 읽혀 사건 순서를 처음부터 센다.</summary>
        public bool hasEventLedger;

        /// <summary>한 번 고른 사건 선택지(방침·대화). 다시 만나면 결과를 보여 준다.</summary>
        public List<string> chosenEventKeys = new();

        /// <summary>다음 일반 사건까지 남은 전환 수.</summary>
        public int normalEventCooldown;

        /// <summary>청산 위기에 밀린 운영 방침이 다음 전환을 기다리는가.</summary>
        public bool policyDeferred;
    }

    [Serializable]
    public sealed class SavedActivePolicy
    {
        public string assetKey;
        public int remainingDays;
    }
}
