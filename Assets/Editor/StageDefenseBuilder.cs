using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.Combat;
using Code.Core;
using Code.Enemies;
using Code.Manager;
using Code.MapCreateSystem;
using Code.Stages;
using Code.UI;
using Code.UI.Popup;
using Code.UI.Stages;
using Code.Units;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>새 전환 씬만 작성한다. 구 씬/프리팹과 이미 작성된 스테이지 수치는 덮어쓰지 않는다.</summary>
public static class StageDefenseBuilder
{
    private const string Root = "Assets/07.SO/Stages";
    private const string ScenePath = "Assets/00.Scenes/StageDefense.unity";
    private const string BarricadePath = "Assets/03.Prefabs/Buildings/StageBarricade.prefab";

    public static void BuildData()
    {
        Guard();
        if (!AssetDatabase.IsValidFolder(Root))
            AssetDatabase.CreateFolder("Assets/07.SO", "Stages");
        string catalogPath = Root + "/StageCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<StageCatalogSO>(catalogPath) == null)
        {
            StageCatalogSO catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            Set(catalog, "Stages", Array.Empty<StageDefinitionSO>());
            Set(catalog, "UpgradeBaseCost", 2);
            Set(catalog, "UpgradeLimit", 3);
            Set(catalog, "DamagePerUpgrade", 0.15f);
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        BuildStageSet();
        ApplyUnlockTable();
        ApplyArtifactTable();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 이미 쓴 7개 스테이지의 모든 웨이브에 파티 크기를 쓴다(2026-11-01 사용자 결정: 순서대로 3명씩 끊어 파티, 임시 값).
    /// 편성·간격·보스 배율은 건드리지 않는다. 새로 쓰는 웨이브(<see cref="BuildStageSet"/>)는 기본값 3을 그대로 받는다.
    /// </summary>
    public static string ApplyPartySize(int size = 3)
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        System.Reflection.PropertyInfo property = typeof(StageWave).GetProperty(nameof(StageWave.PartySize));
        int waves = 0;
        foreach (StageDefinitionSO stage in catalog.Stages)
        {
            if (stage == null || stage.Waves == null)
                continue;
            foreach (StageWave wave in stage.Waves)
            {
                property.SetValue(wave, size);
                waves++;
            }
            EditorUtility.SetDirty(stage);
            // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
            AssetDatabase.SaveAssetIfDirty(stage);
        }
        return $"partySize={size} waves={waves}";
    }

    /// <summary>
    /// 스테이지별 정예(2026-11-01 사용자 결정: 3스테이지부터 보스 직전 웨이브, 스테이지마다 고정 종류, 임시 배치).
    /// 첫 파티는 비우고 둘째 파티부터 하나씩 일반 적 자리를 바꾼다. 전열은 파티 맨 앞, 후열·지원은 맨 뒤 자리다.
    /// </summary>
    private static readonly (string stage, string[] elites)[] EliteTable =
    {
        ("Stage_03", new[] { "EliteShieldGuardEnemy" }),
        ("Stage_04", new[] { "EliteArcherEnemy" }),
        ("Stage_05", new[] { "EliteScoutEnemy" }),
        ("Stage_06", new[] { "EliteHealerEnemy", "EliteShieldGuardEnemy" }),
        ("Stage_07", new[] { "EliteArcherEnemy", "EliteScoutEnemy" })
    };

    /// <summary>
    /// 이미 쓴 스테이지에 <see cref="EliteTable"/>대로 정예를 넣는다. 같은 자리에 다시 써서 여러 번 돌려도 같다.
    /// 모든 스테이지의 자리를 먼저 검사해, 한 곳이라도 틀리면 아무것도 바꾸지 않고 멈춘다.
    /// </summary>
    public static string ApplyElites()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        foreach (StageDefinitionSO stage in catalog.Stages)
            if (stage != null && stage.Waves != null)
                PlaceElites(stage.Id, stage.Waves, false);
        int placed = 0;
        foreach (StageDefinitionSO stage in catalog.Stages)
        {
            if (stage == null || stage.Waves == null)
                continue;
            int count = PlaceElites(stage.Id, stage.Waves, true);
            if (count == 0)
                continue;
            placed += count;
            EditorUtility.SetDirty(stage);
            // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
            AssetDatabase.SaveAssetIfDirty(stage);
        }
        Set(catalog, "Elites", EliteData());
        AssetDatabase.SaveAssetIfDirty(catalog);
        return "elites=" + placed + " catalog=" + catalog.Elites.Length;
    }

    /// <summary>
    /// 스테이지 준비 사건(2026-11-02 사용자 승인, StagePreparationContentDesign.md). 모두 둘째 웨이브 준비에 열린다. 수치는 임시 값.
    /// 선택지 순서가 준비 저장의 선택 번호다 — Stage_01은 예전 "남은 보급품"과 같은 순서(금화·보수·사용 안 함)를 지킨다.
    /// </summary>
    private static StagePreparationEvent PreparationEventFor(string stageId)
    {
        StagePreparationOption skip = new("사용하지 않음", 0, 0);
        return stageId switch
        {
            "Stage_01" => new StagePreparationEvent("남은 보급품", "남은 보급품을 어디에 쓸까요?", "보급품", 1,
                new[] { new StagePreparationOption("금화 +20G", 20, 0), new StagePreparationOption("금고 보수 +2", 0, 2), skip }),
            "Stage_02" => new StagePreparationEvent("도망친 상인의 짐", "도망친 상인이 짐을 두고 갔습니다.", "짐", 1,
                new[] { new StagePreparationOption("금화 +30G", 30, 0), new StagePreparationOption("금고 보수 +2", 0, 2), skip }),
            "Stage_03" => new StagePreparationEvent("무너진 갱도 자재", "갱도 자재로 금고를 보강할 수 있습니다.", "자재", 1,
                new[] { new StagePreparationOption("금화 +25G", 25, 0), new StagePreparationOption("금화로 보강", -20, 3), skip }),
            "Stage_04" => new StagePreparationEvent("불 구멍 옆 석탄", "불 구멍 옆에 석탄이 쌓여 있습니다.", "석탄", 1,
                new[] { new StagePreparationOption("금화 +30G", 30, 0), new StagePreparationOption("금고 보수 +2", 0, 2), skip }),
            "Stage_05" => new StagePreparationEvent("사냥꾼이 남긴 장비", "사냥꾼이 쓰던 장비가 남아 있습니다.", "장비", 1,
                new[] { new StagePreparationOption("금화 +35G", 35, 0), new StagePreparationOption("금화로 보강", -25, 3), skip }),
            "Stage_06" => new StagePreparationEvent("세 갈래 문지기의 뇌물", "문지기가 눈감아 달라며 뇌물을 내밉니다.", "뇌물", 1,
                new[] { new StagePreparationOption("금화 +40G", 40, 0), new StagePreparationOption("금고 보수 +3", 0, 3), skip }),
            "Stage_07" => new StagePreparationEvent("마지막 비축분", "마지막 비축분을 풀 때입니다.", "비축분", 1,
                new[] { new StagePreparationOption("금화 +40G", 40, 0), new StagePreparationOption("금화로 보강", -30, 4), skip }),
            _ => null
        };
    }

    /// <summary>이미 쓴 스테이지에 <see cref="PreparationEventFor"/>의 사건을 쓴다. 스테이지 에셋만 저장한다.</summary>
    public static string ApplyPreparationEvents()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        int written = 0;
        foreach (StageDefinitionSO stage in catalog.Stages)
        {
            StagePreparationEvent preparationEvent = stage != null ? PreparationEventFor(stage.Id) : null;
            if (preparationEvent == null)
                continue;
            if (!preparationEvent.TryValidate(stage.Waves.Length, out string reason))
                throw new InvalidOperationException(stage.Id + ": " + reason);
            Set(stage, "PreparationEvent", preparationEvent);
            // 다른 세션이 편집 중인 에셋까지 저장하지 않게 스테이지만 저장한다
            AssetDatabase.SaveAssetIfDirty(stage);
            written++;
        }
        return "events=" + written;
    }

    /// <summary><see cref="EliteTable"/>에 나오는 정예 종류(중복 없이). 카탈로그 정예 목록이 된다.</summary>
    private static EnemyDataSO[] EliteData()
    {
        List<EnemyDataSO> elites = new();
        foreach ((string _, string[] names) in EliteTable)
            foreach (string name in names)
            {
                EnemyDataSO elite = Load<EnemyDataSO>(Enemies + name + ".asset");
                if (!elites.Contains(elite))
                    elites.Add(elite);
            }
        return elites.ToArray();
    }

    /// <summary>표의 정예 자리를 검사하고, <paramref name="apply"/>가 참이면 그 자리를 바꾼다. 넣을 정예 수를 돌려준다.</summary>
    private static int PlaceElites(string stageId, StageWave[] waves, bool apply)
    {
        foreach ((string stage, string[] elites) in EliteTable)
        {
            if (stage != stageId)
                continue;
            if (waves.Length < 2)
                throw new InvalidOperationException(stageId + ": 보스 직전 웨이브가 없습니다");
            StageWave wave = waves[waves.Length - 2];
            int group = Mathf.Max(1, wave.PartySize);
            for (int i = 0; i < elites.Length; i++)
            {
                EnemyDataSO elite = Load<EnemyDataSO>(Enemies + elites[i] + ".asset");
                bool front = elite.Role is Code.BT.BattleRole.Tank or Code.BT.BattleRole.Melee;
                int slot = (i + 1) * group + (front ? 0 : group - 1);
                if (wave.Enemies == null || slot >= wave.Enemies.Length)
                    throw new InvalidOperationException($"{stageId}: 정예 자리 {slot}가 웨이브 밖입니다");
                // 표식 적 자리는 혼자 움직이므로 정예가 파티와 함께 걷도록 피한다
                if (wave.RuleAt(slot) != StageEnemyRule.None)
                    throw new InvalidOperationException($"{stageId}: 정예 자리 {slot}가 표식 적입니다");
                if (apply)
                    wave.Enemies[slot] = elite;
            }
            return elites.Length;
        }
        return 0;
    }

    private const string ArtifactRoot = "Assets/07.SO/Artifacts/";

    /// <summary>
    /// 스테이지 유물 후보를 쓴다(2026-10-29 사용자 결정: 초회 클리어 3택1·영구 수집·2칸 장착·장착한 둘만 조합).
    /// 물약은 부하가 웨이브 뒤 자동 복구되므로 뺀다. 메인 유닛 전용 인장과 방 조건 늑대 부적은 스테이지 설명만 바꾼다
    /// (에셋은 구 SampleScene이 그대로 쓴다). 칸 수 2·후보 3·늑대 부적 거리 2칸은 임시 값이다.
    /// </summary>
    public static string ApplyArtifactTable()
    {
        Guard();
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        string[] names =
        {
            "QuartermasterLedger", "VeteranBanner", "SharpeningKit", "TowerShield", "IronRations", "FieldMedicSatchel",
            "WarDrum", "SkirmisherBoots", "GlassDagger", "ExecutionOath", "LoneWolfCharm", "CaptainSigil"
        };
        Dictionary<string, string> stageText = new()
        {
            ["CaptainSigil"] = "모든 부하 공격력 +3, 최대 체력 +10.",
            ["LoneWolfCharm"] = "주변 2칸 안에 다른 부하가 없을 때 부하가 주는 피해 ×1.35."
        };
        StageArtifact[] entries = new StageArtifact[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = ArtifactRoot + "GeneratedUnit/" + names[i] + "Artifact.asset";
            stageText.TryGetValue(names[i], out string text);
            entries[i] = new StageArtifact("artifact:" + AssetDatabase.AssetPathToGUID(path),
                Load<Code.Artifacts.ArtifactDataSO>(path), text ?? string.Empty);
        }
        Set(catalog, "Artifacts", entries);
        Set(catalog, "ArtifactCombos", Load<Code.Artifacts.ArtifactComboCatalogSO>(ArtifactRoot + "ArtifactComboCatalog.asset"));
        Set(catalog, "ArtifactSlots", 2);
        Set(catalog, "ArtifactChoices", 3);
        Set(catalog, "LoneAllyRadius", 2f);
        AssetDatabase.SaveAssets();
        return "artifacts=" + entries.Length;
    }

    /// <summary>
    /// 작성된 StageDefense 씬에 유물 칸(편성 패널)과 가운데 유물 창을 더한다. 이미 있으면 지우고 다시 만든다.
    /// 편성 목록은 높이만 줄이고 줄은 그대로 둔다. 설정 ESC 목록에 유물 창을 더한다.
    /// </summary>
    public static string AddArtifacts() => EditScene(scene =>
    {
        StageHudView view = FindInScene<StageHudView>(scene);
        RectTransform host = (RectTransform)view.transform;
        RectTransform side = (RectTransform)host.Find("Preparation");
        if (side == null)
            throw new InvalidOperationException("편성 패널(Preparation)을 찾지 못했습니다");
        RectTransform list = (RectTransform)side.Find(OptionListName);
        if (list != null)
            list.sizeDelta = new Vector2(list.sizeDelta.x, OptionListHeight);
        foreach (Transform old in new[] { side.Find("ArtifactSummary"), side.Find("Artifacts"), host.Find(ArtifactWindowName) })
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
        StageArtifactWindow window = BuildArtifactSection(view, side, host, FindInScene<StageSession>(scene));
        SettingsScreenView settings = FindInScene<SettingsScreenView>(scene, false);
        if (settings != null)
            WireEscape(settings, window);
        return "artifactRows=" + FindInScene<StageSession>(scene).Catalog.Artifacts.Length;
    });

    /// <summary>
    /// 작성된 StageDefense 씬에 부하 명령 막대(2026-11-01 사용자 결정: 사수·기본·추격, 준비 단계·한 명씩)를 더한다.
    /// 이미 있으면 지우고 다시 만든다. 다른 HUD 요소는 건드리지 않는다.
    /// </summary>
    public static string AddCommandBar() => EditScene(scene =>
    {
        StageHudView view = FindInScene<StageHudView>(scene);
        RectTransform host = (RectTransform)view.transform;
        Transform old = host.Find(CommandBarName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        BuildCommandBar(view, host);
        return "commandBar=" + (host.Find(CommandBarName) != null);
    });

    private const string CommandBarName = "CommandBar";
    private const string SupplyButtonName = "Supply";

    /// <summary>
    /// 1스테이지 준비 단계의 보스 소개·사건 팝업(2026-11-02 사용자 승인)을 위해 범용 팝업 프리팹과 보급품 버튼을 놓는다.
    /// 이미 있으면 버튼만 다시 만들고 배선한다.
    /// </summary>
    public static string AddPreparationPopups() => EditScene(scene =>
    {
        PopupView popup = EnsurePopup(scene);
        StageHudView view = FindInScene<StageHudView>(scene);
        RectTransform host = (RectTransform)view.transform;
        Transform old = host.Find(SupplyButtonName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        BuildSupplyButton(view, host);
        return $"popup={popup != null} supply={host.Find(SupplyButtonName) != null}";
    });

    private const string TitleScenePath = "Assets/00.Scenes/Start.unity";
    private const string ClearRecordName = "ClearRecord";

    /// <summary>
    /// 타이틀 메뉴의 종료 버튼 아래에 "모든 스테이지 방어 완료" 기록 글자를 놓고 배선한다(2026-11-02 사용자 결정).
    /// 이미 있으면 다시 만든다. 글은 실행 중에 프로필을 읽어 정한다.
    /// 타이틀이 카탈로그 전체를 올리지 않도록 마지막 스테이지 ID만 써 넣으므로, 스테이지 목록이 바뀌면 다시 돌린다.
    /// </summary>
    public static string AddTitleClearRecord() => EditScene(TitleScenePath, scene =>
    {
        TitleMenuActions menu = FindInScene<TitleMenuActions>(scene);
        SerializedObject serialized = new(menu);
        Component continueButton = serialized.FindProperty("continueButton").objectReferenceValue as Component;
        if (continueButton == null)
            throw new InvalidOperationException("타이틀 이어하기 버튼 배선이 없습니다");
        Transform host = continueButton.transform.parent;
        Transform old = host.Find(ClearRecordName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        TMP_Text record = UiKit.Text(ClearRecordName, host, string.Empty, 18, HudPalette.Accent, TextAlignmentOptions.Center, true, true);
        // 메뉴 버튼들과 같은 가운데 기준으로 종료 버튼(위 -230, 높이 58) 아래에 둔다
        Vector2 center = new(0.5f, 0.5f);
        UiKit.Place(record.rectTransform, center, center, center, new Vector2(0, -300), new Vector2(400, 32));
        record.raycastTarget = false;
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        string finalStageId = catalog.Stages[catalog.Stages.Length - 1].Id;
        UiKit.Wire(menu, "clearRecord", record);
        serialized.Update();
        serialized.FindProperty("finalStageId").stringValue = finalStageId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return "clearRecord=" + (host.Find(ClearRecordName) != null) + " finalStageId=" + finalStageId;
    });

    /// <summary>SampleScene과 같은 범용 팝업 프리팹(캔버스 100)을 놓는다. 팝업 컨트롤러는 처음 쓸 때 스스로 생긴다.</summary>
    private static PopupView EnsurePopup(Scene scene)
    {
        PopupView popup = FindInScene<PopupView>(scene, false);
        if (popup != null)
            return popup;
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PopupBuilder.PrefabPath), scene);
        return instance.GetComponent<PopupView>();
    }

    /// <summary>아래 버튼 줄의 타이틀 버튼 오른쪽(180×54). 사건이 열려 있는 준비 단계에만 HUD가 보인다.</summary>
    private static void BuildSupplyButton(StageHudView view, RectTransform host)
    {
        UiFrameButton button = Button(host, SupplyButtonName, "보급품", 1340, 996, 180, 54);
        UiKit.Wire(view, "supplyButton", button);
        button.gameObject.SetActive(false);
        KeepArtifactWindowOnTop(host);
    }

    /// <summary>
    /// 맵 아래 도구 안내 줄(위 930) 바로 위의 고정 크기 막대(640×48). 고른 부하가 없으면 HUD가 통째로 숨긴다.
    /// 지도 가운데를 가리지 않게 왼쪽 패널 옆에 붙인다.
    /// </summary>
    private static void BuildCommandBar(StageHudView view, RectTransform host)
    {
        RectTransform root = UiKit.TopLeft(UiKit.Rect(CommandBarName, host), 470, 872, 640, 48);
        UiKit.Panel(root, HudPalette.Surface, HudPalette.Border, true);
        StageCommandBar bar = root.gameObject.AddComponent<StageCommandBar>();
        UiKit.Wire(bar, "label", Text(root, "Label", "", 15, 12, 12, 300, 28, HudPalette.TextBody));
        UiKit.Wire(bar, "hold", Button(root, "Hold", "사수", 320, 6, 96, 36));
        UiKit.Wire(bar, "normal", Button(root, "Normal", "기본", 424, 6, 96, 36));
        UiKit.Wire(bar, "pursue", Button(root, "Pursue", "추격", 528, 6, 96, 36));
        UiKit.Wire(view, "commandBar", bar);
        root.gameObject.SetActive(false);
        KeepArtifactWindowOnTop(host);
    }

    /// <summary>가운데 유물 창이 나중에 놓인 HUD 요소에 가려지지 않게 맨 뒤 형제로 다시 둔다.</summary>
    private static void KeepArtifactWindowOnTop(RectTransform host)
    {
        Transform window = host.Find(ArtifactWindowName);
        if (window != null)
            window.SetAsLastSibling();
    }

    private const string ArtifactWindowName = "ArtifactWindow";

    /// <summary>편성 목록 높이. 유물 칸(위 720~826) 자리를 위해 600에서 줄였다.</summary>
    private const float OptionListHeight = 470;

    private static StageArtifactWindow BuildArtifactSection(StageHudView view, RectTransform side, RectTransform host,
        StageSession session)
    {
        UiKit.Wire(view, "artifactSummary", Text(side, "ArtifactSummary", "유물 0/2 · 장착 없음\n조합: 없음", 15, 18, 720, 374, 56, HudPalette.TextBody));
        UiKit.Wire(view, "artifactButton", Button(side, "Artifacts", "유물 장착 · 보상", 18, 782, 374, 44));
        StageArtifactWindow window = BuildArtifactWindow(host, session);
        UiKit.Wire(view, "artifactWindow", window);
        return window;
    }

    /// <summary>
    /// 가운데 고정 크기 창(840×720). 줄은 카탈로그 유물 수만큼 미리 만들고 스크롤한다.
    /// 바깥 오브젝트는 켜 둔 채(세션 변경을 듣는다) 안쪽 패널만 여닫는다.
    /// </summary>
    private static StageArtifactWindow BuildArtifactWindow(RectTransform host, StageSession session)
    {
        RectTransform root = UiKit.Fill(UiKit.Rect(ArtifactWindowName, host));
        StageArtifactWindow window = root.gameObject.AddComponent<StageArtifactWindow>();
        // 보상 모드에서만 켜서 고를 때까지 다른 HUD 조작을 막는다. 장착 모드는 바깥 HUD를 그대로 쓴다.
        RectTransform blocker = UiKit.Fill(UiKit.Rect("Blocker", root));
        UiKit.Image(blocker, null, HudPalette.Scrim, true);
        blocker.gameObject.SetActive(false);
        UiKit.Wire(window, "blocker", blocker.gameObject);
        Vector2 center = new(0.5f, 0.5f);
        RectTransform panel = UiKit.Place(UiKit.Rect("Panel", root), center, center, center, Vector2.zero, new Vector2(840, 720));
        UiKit.Panel(panel, HudPalette.Surface, HudPalette.Border, true);
        UiKit.Wire(window, "session", session);
        UiKit.Wire(window, "panel", panel.gameObject);
        UiKit.Wire(window, "title", Text(panel, "Title", "유물 장착", 24, 24, 20, 600, 40, HudPalette.Accent));
        UiKit.Wire(window, "closeButton", Button(panel, "Close", "닫기", 716, 20, 100, 40));
        UiKit.Wire(window, "hint", Text(panel, "Hint", "", 16, 24, 70, 792, 48, HudPalette.TextDim));
        RectTransform area = UiKit.TopLeft(UiKit.Rect("List", panel), 24, 128, 792, 568);
        UiKit.ScrollArea(area, out RectTransform content);
        UnityEngine.UI.VerticalLayoutGroup layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        StageArtifactRow[] rows = new StageArtifactRow[session.Catalog.Artifacts.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform row = UiKit.Rect("Artifact_" + i, content);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 104;
            rows[i] = row.gameObject.AddComponent<StageArtifactRow>();
            UiKit.Wire(rows[i], "label", Text(row, "Text", "", 15, 0, 0, 630, 100, HudPalette.TextBody));
            UiKit.Wire(rows[i], "action", Button(row, "Action", "장착", 646, 28, 130, 44));
        }
        UiKit.WireArray(window, "rows", rows);
        panel.gameObject.SetActive(false);
        // 나중에 놓인 HUD 요소가 창을 덮지 않게 맨 뒤 형제로 둔다.
        root.SetAsLastSibling();
        return window;
    }

    /// <summary>ESC는 설정 → 유물 창 순으로 하나씩 닫는다. 고르지 않은 보상이 있으면 유물 창은 닫히지 않는다.</summary>
    private static void WireEscape(SettingsScreenView settings, StageArtifactWindow window)
    {
        UiEscapeRouter router = settings.GetComponent<UiEscapeRouter>();
        UiKit.Wire(router, "settings", settings);
        UiKit.WireArray(router, "windows", window != null
            ? new UnityEngine.Object[] { settings, window }
            : new UnityEngine.Object[] { settings });
    }

    private const string Enemies = "Assets/07.SO/Enemies/";

    /// <summary>
    /// 스테이지 7개를 구성표대로 쓴다(2026-10-28 사용자 결정, StageDefenseDesign.md "스테이지 구성 확정").
    /// 이미 있는 Stage_01·02도 같은 GUID로 덮어써 클리어 기록 ID가 이어진다. 기믹은 도중에 열리는 문·설치 불가/강화 칸·표식 적·환경 장치(<see cref="DevicesFor"/>)다.
    /// 마지막 웨이브는 보스 + 호위(기존 보스 파티에서 셋 이하). 맵·웨이브·보스 도달 피해 5·배율(체력 ×6, 공격 ×2)은 모두 임시 값이다.
    /// </summary>
    public static string BuildStageSet()
    {
        Guard();
        EnemyDataSO basic = Load<EnemyDataSO>(Enemies + "BasicEnemy.asset");
        EnemyDataSO sword = Load<EnemyDataSO>(Enemies + "SwordEnemy.asset");
        EnemyDataSO archer = Load<EnemyDataSO>(Enemies + "ArcherEnemy.asset");
        EnemyDataSO healer = Load<EnemyDataSO>(Enemies + "HealterEnemy.asset");
        EnemyDataSO[] pool = { basic, sword, archer, basic, sword, healer, archer };
        Vector2Int[] oneDoor = { new(1, 5) };
        Vector2Int[] twoDoors = { new(1, 2), new(1, 9) };
        Vector2Int[] threeDoors = { new(1, 2), new(1, 5), new(1, 9) };
        string[] pillars =
        {
            "#######################",
            "#.....................#",
            "#.....................#",
            "#.....##.......##.....#",
            "#.....##.......##.....#",
            "#.....................#",
            "#.....................#",
            "#.....##.......##.....#",
            "#.....##.......##.....#",
            "#.....................#",
            "#.....................#",
            "#######################"
        };
        string[] lanes =
        {
            "#######################",
            "#.....................#",
            "#......##.....##......#",
            "#......##.....##......#",
            "#.....................#",
            "#......##.....##......#",
            "#......##.....##......#",
            "#.....................#",
            "#......##.....##......#",
            "#......##.....##......#",
            "#.....................#",
            "#######################"
        };
        string[] fork =
        {
            "#######################",
            "#.....................#",
            "#.........###.........#",
            "#.........###.........#",
            "#.....................#",
            "#.........###.........#",
            "#.........###.........#",
            "#.....................#",
            "#.........###.........#",
            "#.........###.........#",
            "#.....................#",
            "#######################"
        };
        string[] zigzag =
        {
            "#######################",
            "#.....................#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#.....#...#...#...#...#",
            "#.........#.......#...#",
            "#.........#.......#...#",
            "#.....#...#...#...#...#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#.....................#",
            "#######################"
        };
        string[] scattered =
        {
            "#######################",
            "#.....................#",
            "#.....................#",
            "#....##....##....##...#",
            "#.....................#",
            "#.....................#",
            "#.....................#",
            "#.....................#",
            "#....##....##....##...#",
            "#.....................#",
            "#.....................#",
            "#######################"
        };
        // 'x' 설치 불가 바닥, '+' 강화 칸(함정 피해 ×1.5)
        string[] forkCells =
        {
            "#######################",
            "#.....................#",
            "#...xx....###....++...#",
            "#.........###.........#",
            "#.....+...............#",
            "#...xx....###....xx...#",
            "#.........###.........#",
            "#.....+...............#",
            "#.........###....++...#",
            "#...xx....###.........#",
            "#.....................#",
            "#######################"
        };
        string[] zigzagCells =
        {
            "#######################",
            "#..xx.....++.....xx...#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#.....#...#...#...#...#",
            "#....+....#..+....#...#",
            "#.........#.......#...#",
            "#.....#...#...#...#...#",
            "#.....#.......#.......#",
            "#.....#.......#.......#",
            "#..xx.....++.....xx...#",
            "#######################"
        };
        // 문마다 처음 열리는 웨이브(1부터). 마지막 문이 3웨이브부터 열린다.
        int[] always = Array.Empty<int>();
        int[] secondOpensAt3 = { 1, 3 };
        int[] thirdOpensAt3 = { 1, 1, 3 };
        (string id, string title, string gimmick, string[] layout, Vector2Int[] doors, int[] opens, bool marked, int waves, string boss, string party)[] specs =
        {
            ("Stage_01", "첫 금고", "연습: 문 하나, 기믹 없음", pillars, oneDoor, always, false, 3, "Traits/Boss_GreedKnight", "Traits/BossParty_GreedKnight"),
            ("Stage_02", "두 개의 침입로", "도중에 열리는 문: 3웨이브부터 아래 문", lanes, twoDoors, secondOpensAt3, false, 4, "Bosses/Boss_BanditRider", "Bosses/BossParty_Rotation_BanditRider"),
            ("Stage_03", "갈림길의 문", "붉은 바닥 설치 불가 · 파란 바닥 함정 피해 +50%", forkCells, oneDoor, always, false, 4, "Bosses/Boss_KnightCommander", "Bosses/BossParty_Day20"),
            ("Stage_04", "구불길", "환경 장치: 불 구멍 · 여닫는 문", zigzag, oneDoor, always, false, 5, "Bosses/Boss_Archmage", "Bosses/BossParty_Rotation_Archmage"),
            ("Stage_05", "사냥터", "표식 적: 도약(바리케이드를 넘음) · 돌진(부하와 싸우지 않음)", scattered, twoDoors, always, true, 5, "Bosses/Boss_Houndmaster", "Bosses/BossParty_Day18"),
            ("Stage_06", "세 갈래 문", "3웨이브부터 새 문 + 불 구멍 · 여닫는 문 · 낙석", lanes, threeDoors, thirdOpensAt3, false, 6, "Bosses/Boss_OrcWarchief", "Bosses/BossParty_Rotation_OrcWarchief"),
            ("Stage_07", "마지막 금고", "새 문 · 칸 · 표식 적 · 환경 장치 전부", zigzagCells, threeDoors, thirdOpensAt3, true, 6, "Bosses/Boss_Subjugator", "Bosses/BossParty_Day9")
        };
        List<StageDefinitionSO> stages = new();
        for (int index = 0; index < specs.Length; index++)
        {
            (string id, string title, string gimmick, string[] layout, Vector2Int[] doors, int[] opens, bool marked, int waves, string boss, string party) spec = specs[index];
            EnemyDataSO boss = Load<EnemyDataSO>(Enemies + spec.boss + ".asset");
            Set(boss, "CoreArrivalDamage", 5);
            EnemyDataSO[] members = Load<AdventurerPartySO>("Assets/07.SO/Waves/" + spec.party + ".asset").Members;
            EnemyDataSO[] escorts = new EnemyDataSO[Mathf.Min(3, members.Length - 1)];
            Array.Copy(members, 1, escorts, 0, escorts.Length);
            StageWave[] waves = new StageWave[spec.waves];
            float interval = Mathf.Max(0.9f, 1.4f - index * 0.05f);
            for (int wave = 0; wave < spec.waves; wave++)
            {
                bool last = wave == spec.waves - 1;
                EnemyDataSO[] regular = new EnemyDataSO[last ? 3 : 4 + index + wave];
                // 표식 스테이지는 일반 적 셋 중 하나에 도약/돌진을 웨이브마다 번갈아 붙인다(임시)
                StageEnemyRule[] rules = new StageEnemyRule[spec.marked ? regular.Length : 0];
                for (int i = 0; i < regular.Length; i++)
                {
                    regular[i] = pool[(i + wave + index) % pool.Length];
                    if (spec.marked && i % 3 == 1)
                        rules[i] = (i / 3 + wave) % 2 == 0 ? StageEnemyRule.Vault : StageEnemyRule.Rush;
                }
                waves[wave] = last ? new StageWave(regular, interval, boss, escorts, rules) : new StageWave(regular, interval, rules);
            }
            PlaceElites(spec.id, waves, true);
            StageDefinitionSO stage = WriteStage(spec.id, spec.title, spec.gimmick, spec.layout, spec.doors, new Vector2Int(21, 5), waves);
            Set(stage, "DoorOpensAtWave", spec.opens);
            Set(stage, "Devices", DevicesFor(spec.id));
            StageHallBuilder.ApplyShape(stage, index + 1);
            Set(stage, "EmpoweredTrapMultiplier", 1.5f);
            Set(stage, "PreparationEvent", PreparationEventFor(spec.id));
            stages.Add(stage);
        }
        StageCatalogSO catalog = Load<StageCatalogSO>(Root + "/StageCatalog.asset");
        Set(catalog, "Stages", stages.ToArray());
        Set(catalog, "Elites", EliteData());
        AssetDatabase.SaveAssets();
        return "stages=" + stages.Count;
    }

    /// <summary>
    /// 편성 선택지를 해금표대로 다시 쓴다(2026-10-28 사용자 결정, StageDefenseDesign.md "해금 배정").
    /// 부하 9종·함정 7종, 시작 4개, 클리어마다 다음 기믹 대비 종류를 푼다. 슬롯은 기본 4에 Stage_02·Stage_04 첫 클리어마다 +1.
    /// 선택지 ID는 에셋 GUID라 기존 강화 기록은 그대로 이어진다. 강화 비용·상한·피해 배율은 건드리지 않는다.
    /// </summary>
    public static string ApplyUnlockTable()
    {
        Guard();
        StageCatalogSO catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(Root + "/StageCatalog.asset");
        if (catalog == null)
            return "카탈로그가 없습니다";
        const string units = "Assets/07.SO/Units/";
        const string traps = "Assets/07.SO/Buildings/";
        StageOption[] options =
        {
            UnitOption(units + "Generated/GuardianUnitData.asset", ""),
            UnitOption(units + "SlimeBlueUnitData.asset", ""),
            TrapOption(traps + "SnareCrystalTrapBuildingData.asset", ""),
            TrapOption(traps + "FireBrazierTrapBuildingData.asset", ""),
            UnitOption(units + "Generated/WolfUnitData.asset", "Stage_01"),
            UnitOption(units + "Generated/ArbalistUnitData.asset", "Stage_01"),
            TrapOption(traps + "ManaCrystalTrapBuildingData.asset", "Stage_01"),
            TrapOption(traps + "ShockCrystalTrapBuildingData.asset", "Stage_02"),
            TrapOption(traps + "BloodPoolTrapBuildingData.asset", "Stage_02"),
            UnitOption(units + "Generated/BattleMageUnitData.asset", "Stage_03"),
            UnitOption(units + "Generated/VanguardUnitData.asset", "Stage_03"),
            UnitOption(units + "SkeletonSniperUnitData.asset", "Stage_04"),
            TrapOption(traps + "ExposeCrystalTrapBuildingData.asset", "Stage_04"),
            UnitOption(units + "Generated/ShroomUnitData.asset", "Stage_05"),
            TrapOption(traps + "StormCloudTrapBuildingData.asset", "Stage_05"),
            UnitOption(units + "Generated/ShadeUnitData.asset", "Stage_06")
        };
        int before = catalog.Options?.Length ?? 0;
        Set(catalog, "Options", options);
        Set(catalog, "BaseLoadoutSlots", 4);
        Set(catalog, "SlotRewardStages", new[] { "Stage_02", "Stage_04" });
        return $"options {before} -> {options.Length}";
    }

    /// <summary>
    /// 이미 작성된 StageDefense 씬의 편성 목록을 카탈로그 선택지 수에 맞춘 스크롤 목록으로 다시 만든다.
    /// 다른 HUD 요소는 건드리지 않는다. 씬이 열려 있고 저장하지 않은 변경이 있으면 멈춘다.
    /// </summary>
    public static string RebuildOptionList() => EditScene(scene =>
    {
        StageHudView view = FindInScene<StageHudView>(scene);
        RectTransform side = (RectTransform)view.transform.Find("Preparation");
        if (side == null)
            throw new InvalidOperationException("편성 패널(Preparation)을 찾지 못했습니다");
        for (int i = side.childCount - 1; i >= 0; i--)
        {
            Transform child = side.GetChild(i);
            if (child.name.StartsWith("Option_", StringComparison.Ordinal) || child.name == OptionListName)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        StageOptionRow[] rows = BuildOptionList(side, FindInScene<StageSession>(scene).Catalog);
        UiKit.WireArray(view, "rows", rows);
        return $"rows={rows.Length}";
    });

    /// <summary>
    /// 이미 작성된 StageDefense 씬에 배속 컨트롤러·설정 화면(ESC)·오른쪽 위 배속/설정 버튼을 더한다.
    /// 이미 있으면 다시 만들지 않고 배선만 다시 한다. 다른 HUD 요소는 제목·상태 글 폭만 줄인다(버튼 자리).
    /// </summary>
    public static string AddSpeedAndSettings() => EditScene(scene =>
    {
        StageHudView view = FindInScene<StageHudView>(scene);
        GameSpeedController speed = EnsureSpeedController(scene);
        SettingsScreenView settings = EnsureSettingsScreen(scene, speed);
        UiKit.Wire(FindInScene<StageSession>(scene), "speedController", speed);
        RectTransform host = (RectTransform)view.transform;
        foreach (string name in new[] { "StageTitle", "Status" })
        {
            RectTransform text = (RectTransform)host.Find(name);
            if (text != null)
                text.sizeDelta = new Vector2(HeadlineWidth, text.sizeDelta.y);
        }
        foreach (string name in SpeedButtonNames)
        {
            Transform old = host.Find(name);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        BuildSpeedButtons(view, host, speed, settings);
        return "speed=" + (speed != null) + " settings=" + (settings != null);
    });

    private static readonly string[] SpeedButtonNames = { "Pause", "NormalSpeed", "FastSpeed", "Settings" };

    /// <summary>제목·상태 글 폭. 오른쪽 위 배속/설정 버튼(1480~1888)과 겹치지 않게 470~1460.</summary>
    private const float HeadlineWidth = 990;

    private static void BuildSpeedButtons(StageHudView view, RectTransform host, GameSpeedController speed,
        SettingsScreenView settings)
    {
        UiKit.Wire(view, "speedController", speed);
        UiKit.Wire(view, "settings", settings);
        UiKit.Wire(view, "pauseButton", Button(host, "Pause", "일시정지", 1480, 28, 120, 44));
        UiKit.Wire(view, "normalSpeedButton", Button(host, "NormalSpeed", "1배", 1608, 28, 72, 44));
        UiKit.Wire(view, "fastSpeedButton", Button(host, "FastSpeed", "2배", 1688, 28, 72, 44));
        UiKit.Wire(view, "settingsButton", Button(host, "Settings", "설정", 1768, 28, 120, 44));
    }

    private static GameSpeedController EnsureSpeedController(Scene scene)
    {
        GameSpeedController speed = FindInScene<GameSpeedController>(scene, false);
        if (speed != null)
            return speed;
        GameObject host = new("Game Speed");
        SceneManager.MoveGameObjectToScene(host, scene);
        return host.AddComponent<GameSpeedController>();
    }

    /// <summary>
    /// SampleScene과 같은 설정 화면 프리팹을 놓는다. ESC 라우터는 이 씬에 다른 창이 없어 설정 하나만 닫고 연다.
    /// "다시 시작"은 구 런 저장을 지우고 SampleScene으로 가므로 끈다(스테이지 HUD의 재도전을 쓴다).
    /// </summary>
    private static SettingsScreenView EnsureSettingsScreen(Scene scene, GameSpeedController speed)
    {
        SettingsScreenView settings = FindInScene<SettingsScreenView>(scene, false);
        if (settings == null)
        {
            GameObject prefab = Load<GameObject>(SettingsBuilder.PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            settings = instance.GetComponent<SettingsScreenView>();
        }
        UiKit.Wire(settings, "speedController", speed);
        SerializedObject serialized = new(settings);
        serialized.FindProperty("allowRunRestart").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        WireEscape(settings, FindInScene<StageArtifactWindow>(scene, false));
        return settings;
    }

    /// <summary>
    /// 작성된 StageDefense 씬을 열어(이미 열려 있으면 그대로) 고치고 저장한다. 저장하지 않은 변경이 있으면 멈춘다.
    /// 다른 열린 씬은 건드리지 않는다.
    /// </summary>
    private static string EditScene(Func<Scene, string> edit) => EditScene(ScenePath, edit);

    private static string EditScene(string path, Func<Scene, string> edit)
    {
        Guard();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException(path + " 씬에 저장하지 않은 변경이 있어 멈춥니다");
        if (opened)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            string result = edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(path + " 씬 저장 실패");
            return result;
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static T FindInScene<T>(Scene scene, bool required = true) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        if (required)
            throw new InvalidOperationException(typeof(T).Name + "을(를) 찾지 못했습니다");
        return null;
    }

    /// <summary>
    /// 환경 장치 배치(2026-10-28 사용자 결정: 맵 고정, 4스테이지부터 불 구멍·여닫는 문, 6·7스테이지에 낙석을 더함).
    /// 임시 값: 불 구멍 6초마다 2초 켜짐·0.5초마다 피해 2, 여닫는 문 8초 열림·8초 닫힘, 낙석 반경 1.5칸·피해 6·대기 20초.
    /// 문 칸은 닫혀도 다른 통로가 남는 자리다(스테이지 선택 때 다시 확인한다).
    /// </summary>
    private static StageDevice[] DevicesFor(string id)
    {
        static StageDevice Vent(int x, int y, float delay) => new(StageDeviceKind.FireVent, new Vector2Int(x, y), 6f, 2f, delay, 0.5f, 2, 0f, 0f);
        static StageDevice Gate(int x, int y, float delay) => new(StageDeviceKind.Gate, new Vector2Int(x, y), 16f, 8f, delay, 0.5f, 0, 0f, 0f);
        static StageDevice Rockfall(int x, int y) => new(StageDeviceKind.Rockfall, new Vector2Int(x, y), 1f, 0f, 0f, 0.5f, 6, 1.5f, 20f);
        return id switch
        {
            "Stage_04" => new[] { Vent(8, 5, 0f), Vent(16, 6, 3f), Gate(6, 5, 0f) },
            "Stage_06" => new[] { Vent(11, 1, 0f), Vent(11, 10, 3f), Gate(11, 7, 0f), Rockfall(19, 2) },
            "Stage_07" => new[] { Vent(8, 6, 0f), Vent(16, 5, 3f), Gate(6, 6, 0f), Gate(14, 1, 4f), Rockfall(19, 2) },
            _ => Array.Empty<StageDevice>()
        };
    }

    /// <summary>스테이지 에셋을 쓴다. 이미 있으면 같은 에셋(GUID)에 덮어쓴다.</summary>
    private static StageDefinitionSO WriteStage(string id, string title, string gimmick, string[] layout,
        Vector2Int[] doors, Vector2Int core, StageWave[] waves)
    {
        string path = Root + "/" + id + ".asset";
        StageDefinitionSO existing = AssetDatabase.LoadAssetAtPath<StageDefinitionSO>(path);
        StageDefinitionSO stage = existing != null ? existing : ScriptableObject.CreateInstance<StageDefinitionSO>();
        (string, object)[] settings =
        {
            ("Id", id), ("Title", title), ("Gimmick", gimmick), ("Layout", layout), ("Doors", doors), ("Core", core),
            ("CoreHealth", 10), ("StartingGold", 250), ("CellSize", 1.25f),
            ("UnitSpeed", 2.5f), ("EnemySpeed", 1.6f), ("EngageRadius", 3.5f), ("LeashRadius", 4f),
            ("BarricadeCost", 10), ("BarricadeHealth", 8), ("RecoveryDelay", 3f), ("RecoveryPerSecond", 1f),
            ("DestroyRefund", 0.25f), ("DemolishRefund", 0.5f), ("ClearReward", 3), ("KillReward", 5), ("Waves", waves)
        };
        foreach ((string property, object value) in settings)
            Set(stage, property, value);
        if (existing == null)
            AssetDatabase.CreateAsset(stage, path);
        return stage;
    }

    public static void BuildBarricade()
    {
        Guard();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BarricadePath) != null)
            return;
        RoomTileSet tileSet = Load<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
        GameObject root = new("StageBarricade");
        try
        {
            Health health = root.AddComponent<Health>();
            Combatant combat = root.AddComponent<Combatant>();
            UiKit.Wire(combat, "health", health);
            StageBarricade barricade = root.AddComponent<StageBarricade>();
            SpriteRenderer visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(root.transform, false);
            visual.sprite = tileSet.Wall.m_DefaultSprite;
            visual.sortingOrder = 12;
            if (visual.sprite != null)
                visual.transform.localScale = Vector3.one * (1.25f / visual.sprite.bounds.size.x);
            SpriteRenderer background = SpriteBar("HealthTrack", root.transform, new Color(0.08f, 0.06f, 0.1f), 0.9f, 0.1f);
            background.transform.localPosition = new Vector3(0, 0.7f, 0);
            SpriteRenderer fill = SpriteBar("HealthFill", root.transform, new Color(0.45f, 0.9f, 0.55f), 0.9f, 0.1f);
            fill.transform.localPosition = new Vector3(0, 0.7f, -0.01f);
            fill.sortingOrder++;
            StageHealthBar bar = root.AddComponent<StageHealthBar>();
            UiKit.Wire(bar, "health", health);
            UiKit.Wire(bar, "fill", fill.transform);
            PrefabUtility.SaveAsPrefabAsset(root, BarricadePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static void BuildScene()
    {
        Guard();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("이미 있는 StageDefense 씬은 자동으로 덮어쓰지 않습니다");
        Scene previous = SceneManager.GetActiveScene();
        RuntimeAssetInstaller sourceInstaller = UnityEngine.Object.FindFirstObjectByType<RuntimeAssetInstaller>();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Camera camera = new GameObject("Main Camera", typeof(AudioListener)).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 12;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.022f, 0.04f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            Light2D light = new GameObject("Stage Light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerRadius = 35f;
            light.pointLightOuterRadius = 40f;
            light.intensity = 0.9f;
            if (sourceInstaller != null)
            {
                RuntimeAssetInstaller installer = new GameObject("Runtime Assets").AddComponent<RuntimeAssetInstaller>();
                EditorUtility.CopySerialized(sourceInstaller, installer);
            }
            StageSession session = new GameObject("Stage Session").AddComponent<StageSession>();
            Transform actors = new GameObject("Actors").transform;
            StageMapView map = new GameObject("Stage Map").AddComponent<StageMapView>();
            Grid grid = new GameObject("Grid").AddComponent<Grid>();
            grid.transform.SetParent(map.transform);
            Tilemap tilemap = new GameObject("Terrain", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            tilemap.transform.SetParent(grid.transform, false);
            tilemap.GetComponent<TilemapRenderer>().sortingOrder = 0;
            RoomTileSet tileSet = Load<RoomTileSet>("Assets/07.SO/Map/RoomTileSet.asset");
            SpriteRenderer core = new GameObject("Core").AddComponent<SpriteRenderer>();
            core.sortingOrder = 10;
            core.color = new Color(0.65f, 0.8f, 1f);
            core.sprite = Load<BuildingDataSO>("Assets/07.SO/Buildings/TreasuryBuildingData.asset").BoardSprite;
            if (core.sprite == null)
                core.sprite = tileSet.Floor.m_DefaultSprite;
            Health coreHealth = core.gameObject.AddComponent<Health>();
            SpriteRenderer door = new GameObject("Door Template").AddComponent<SpriteRenderer>();
            door.sortingOrder = 10;
            door.sprite = tileSet.DoorFrames.Length > 0 ? tileSet.DoorFrames[0] : tileSet.Wall.m_DefaultSprite;
            door.gameObject.SetActive(false);
            Transform doors = new GameObject("Dungeon Doors").transform;
            UiKit.Wire(map, "grid", grid);
            UiKit.Wire(map, "tiles", tilemap);
            UiKit.Wire(map, "floor", tileSet.Floor);
            UiKit.Wire(map, "wall", tileSet.Wall);
            UiKit.Wire(map, "worldCamera", camera);
            UiKit.Wire(map, "coreSprite", core);
            UiKit.Wire(map, "doorPrefab", door);
            UiKit.Wire(map, "doorsRoot", doors);
            UiKit.Wire(map, "<CoreHealth>k__BackingField", coreHealth);
            UiKit.Wire(session, "<Catalog>k__BackingField", Load<StageCatalogSO>(Root + "/StageCatalog.asset"));
            UiKit.Wire(session, "mapView", map);
            UiKit.Wire(session, "actorsRoot", actors);
            UiKit.Wire(session, "barricadePrefab", Load<GameObject>(BarricadePath).GetComponent<StageBarricade>());
            // 작성된 씬에서도 고정 지형을 편집할 수 있게 최초 맵을 직접 칠한다. 플레이/시뮬레이션은 하지 않는다.
            StageDefinitionSO initial = session.Catalog.Stages[0];
            StageBoard board = new(initial);
            StageHallBuilder.ConfigureMap(map);
            map.PaintLayout(board);
            BuildHud(session, camera);
            StageHudView hud = FindInScene<StageHudView>(scene);
            GameSpeedController speed = EnsureSpeedController(scene);
            UiKit.Wire(session, "speedController", speed);
            BuildSpeedButtons(hud, (RectTransform)hud.transform, speed, EnsureSettingsScreen(scene, speed));
            EnsurePopup(scene);
            GameObject events = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("새 스테이지 씬 저장 실패");
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            if (!scenes.Exists(entry => entry.path == ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
    }

    private static void BuildHud(StageSession session, Camera camera)
    {
        GameObject root = new("Stage HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.UI.CanvasScaler scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform host = (RectTransform)root.transform;
        StageHudView view = root.AddComponent<StageHudView>();
        UiKit.Wire(view, "session", session);
        UiKit.Wire(view, "worldCamera", camera);
        RectTransform side = UiKit.TopLeft(UiKit.Rect("Preparation", host), 20, 20, 410, 1040);
        UiKit.Panel(side, HudPalette.Surface, HudPalette.Border, true);
        Text(side, "Title", "방어 편성", 22, 18, 18, 240, 40, HudPalette.Accent);
        UiKit.Wire(view, "previousStage", Button(side, "PreviousStage", "<", 270, 22, 54, 32));
        UiKit.Wire(view, "nextStage", Button(side, "NextStage", ">", 332, 22, 54, 32));
        UiKit.Wire(view, "loadout", Text(side, "Loadout", "편성을 선택하세요", 17, 18, 70, 374, 40, HudPalette.TextBody));
        Text(side, "Hint", "편성 추가 → 종류 선택 → 바닥에 배치\n첫 웨이브 이후 편성은 고정됩니다", 15, 18, 112, 374, 54, HudPalette.TextDim);
        UiKit.Wire(view, "barricade", Button(side, "Barricade", "바리케이드", 18, 176, 177, 44));
        UiKit.Wire(view, "demolish", Button(side, "Demolish", "철거", 211, 176, 177, 44));
        UiKit.WireArray(view, "rows", BuildOptionList(side, session.Catalog));
        BuildArtifactSection(view, side, host, session);
        UiKit.Wire(view, "message", Text(side, "Message", "", 16, 18, 864, 374, 142, HudPalette.Warn));
        UiKit.Wire(view, "headline", Text(host, "StageTitle", "스테이지", 24, 470, 28, HeadlineWidth, 42, HudPalette.Accent));
        UiKit.Wire(view, "status", Text(host, "Status", "", 22, 470, 82, HeadlineWidth, 42, HudPalette.TextBody));
        UiKit.Wire(view, "tool", Text(host, "Tool", "", 18, 470, 930, 1400, 38, HudPalette.TextBody));
        BuildCommandBar(view, host);
        UiKit.Wire(view, "primary", Button(host, "Primary", "웨이브 시작", 470, 996, 300, 54));
        UiKit.Wire(view, "retry", Button(host, "Retry", "스테이지 재도전", 800, 996, 300, 54));
        UiKit.Wire(view, "title", Button(host, "Title", "타이틀", 1130, 996, 180, 54));
        BuildSupplyButton(view, host);
        SpriteRenderer cursor = SpriteBar("Placement Cursor", null, new Color(0.3f, 0.9f, 0.6f, 0.3f), 1, 1);
        cursor.sortingOrder = 100;
        UiKit.Wire(view, "cursor", cursor);
        KeepArtifactWindowOnTop(host);
    }

    private const string OptionListName = "OptionList";

    /// <summary>
    /// 편성 선택지 줄을 스크롤 목록에 쌓는다. 자리는 예전 6줄 자리(위 240~840)이고 선택지가 늘면 스크롤된다 —
    /// 고정 6줄일 때 선택지가 늘면 HUD가 넘친 줄을 묶지 않아 조용히 사라졌다.
    /// </summary>
    private static StageOptionRow[] BuildOptionList(RectTransform side, StageCatalogSO catalog)
    {
        RectTransform area = UiKit.TopLeft(UiKit.Rect(OptionListName, side), 18, 240, 386, OptionListHeight);
        UiKit.ScrollArea(area, out RectTransform content);
        UnityEngine.UI.VerticalLayoutGroup layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        StageOptionRow[] rows = new StageOptionRow[catalog.Options.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform row = UiKit.Rect("Option_" + i, content);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 90;
            rows[i] = row.gameObject.AddComponent<StageOptionRow>();
            UiKit.Wire(rows[i], "label", Text(row, "Name", catalog.Options[i].Label, 15, 0, 0, 310, 32, HudPalette.TextBody));
            UiKit.Wire(rows[i], "remove", Button(row, "Remove", "해제", 316, 0, 58, 30));
            UiKit.Wire(rows[i], "pick", Button(row, "Pick", "편성 추가", 0, 38, 178, 40));
            UiKit.Wire(rows[i], "upgrade", Button(row, "Upgrade", "강화", 194, 38, 178, 40));
        }
        return rows;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size,
        float x, float y, float width, float height, Color color)
    {
        TMP_Text text = UiKit.Text(name, parent, value, size, color, TextAlignmentOptions.TopLeft);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    private static UiFrameButton Button(Transform parent, string name, string label,
        float x, float y, float width, float height)
    {
        return UiKit.Button(UiKit.TopLeft(UiKit.Rect(name, parent), x, y, width, height), label, UiKit.ButtonKind.Secondary, 16);
    }

    private static SpriteRenderer SpriteBar(string name, Transform parent, Color color, float width, float height)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = UiKit.FillSprite;
        renderer.color = color;
        renderer.sortingOrder = 40;
        Vector2 bounds = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(width / bounds.x, height / bounds.y, 1);
        return renderer;
    }

    private static StageOption UnitOption(string path, string required) => new("unit:" + AssetDatabase.AssetPathToGUID(path), Load<UnitDataSO>(path), null, required);
    private static StageOption TrapOption(string path, string required) => new("trap:" + AssetDatabase.AssetPathToGUID(path), null, Load<BuildingDataSO>(path), required);

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new InvalidOperationException("필수 에셋이 없습니다: " + path);
    }

    private static void Set(UnityEngine.Object target, string property, object value)
    {
        target.GetType().GetProperty(property).SetValue(target, value);
        EditorUtility.SetDirty(target);
    }

    private static void Guard()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("편집 모드에서 컴파일 완료 후 실행하세요");
    }
}
