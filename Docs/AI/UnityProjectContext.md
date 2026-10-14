# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project summary
- Root: `C:\Fork\Defence`; first analyzed 2026-09-07, facts corrected 2026-10-14 (Asia/Seoul). `CLAUDE.md` is authoritative when this file disagrees.
- Confirmed: 2D dungeon management and automated combat. Prepare a node-based dungeon, hire/deploy units and facilities, start adventurer waves, intervene with commands/powers, settle income/upkeep, then expand.
- Distinctive tradeoff: Store increases enemy attack, Inn heals enemies, and Blacksmith increases enemy attack/defense in exchange for facility income. Adventurer traits affect spending, fear, retreat and support healing. A room either earns (facility where adventurers dwell) or guards (units), never both.
- Current scene starts with 200 gold. Debt runs through three weekly loan products (back-alley loan, basic loan, merchant guild loan) chosen on settlement days. The scene-linked `WaveConfig.asset` is endless (`finalDay: 0`, `bossEveryNDays: 7`): days 1–28 are hand-authored waves and bosses arrive on days 7, 14, 21, 28. Do not confuse these serialized values with C# defaults (100 starting gold, final day 50).
- Every 7th day is a settlement day: weekly interest and a minimum repayment are due. Missing it (or exceeding the credit limit at warning level 5) triggers bankruptcy; main-unit defeat triggers game over. The victory path (`WaveManager` final-wave clear) only runs when `finalDay > 0`, so the current run has no victory.

## Environment and frameworks
- Confirmed Unity 6000.6.3f1, revision 45d8eee7de74.
- URP 17.6.0, Renderer2D: all quality levels reference UniversalRP.asset, which references Renderer2D.asset. Global GraphicsSettings pipeline is null, but quality overrides establish URP.
- New Input System 1.20.0 only (`activeInputHandler: 1`).
- Unity Behavior 1.0.16; Behavior graph actions call BattleAgent. Character prefabs inspected serialize `autoDrive: 0`; code also supports an automatic fallback driver.
- uGUI/TextMesh Pro, UI Toolkit, DOTween and Feel/MMFeedbacks are present. Test Framework 1.8.0.
- No first-party Netcode/Mirror/Photon API use found; Multiplayer Center is not evidence of multiplayer gameplay.
- Windows Build Profile exists; supported shipping platforms remain undocumented.

## Directory and assembly boundaries
- `Assets/Scenes`: Start and SampleScene are the current scene assets; Build Settings enables them in that order.
- `Assets/Code`: gameplay runtime code; features include Manager, MapCreateSystem, BT, Combat, Units, Enemies, Buildings, Skills, StatusEffects, Progression, Artifacts, Persistence, Dialogue and UI.
- `Assets/GameModules/Data`: gameplay definitions; `Assets/GameModules/Prefabs`: gameplay/UI prefabs; `Settings`: render/build assets. `Assets/Scenes` contains the game scenes and `Assets/_Graphics` contains art and fonts.
- `Assets/GameLib/Entity`: reusable module owner, stats and sensing primitives. It deliberately has no dependency on gameplay code.
- Imported assets live in Feel, Plugins, csiimnida, vHierarchy and other vendor folders. Generated Library/Temp/Logs/obj content is not authoritative source.
- `DungeonKeeper.GameLib` owns reusable entity primitives; `DungeonKeeper.Runtime` owns gameplay code and depends on GameLib. Vendor assemblies remain separate.
- Defence.EditMode.Tests is Editor-only, references `DungeonKeeper.Runtime`, and uses reflection with Unity test runners and NUnit references.

## Startup and gameplay flow
- Enabled build order: Start.unity -> SampleScene.unity; other first-party scenes are development scenes.
- StartMenuController.StartGame loads SampleScene. StartNewGame deletes the checkpoint before loading; normal startup can resume a saved run.
- DungeonGraphController.Awake builds the initial graph; Start waits one frame before TryRestoreCurrentRun.
- DayManager permits a new wave only while in standby, with a portal and WaveManager approval. DayChangedEvent drives spawning; WaveEndedEvent returns to standby and schedules checkpoint saving one frame later.
- WaveManager handles party composition, threat preview, groups, boss phases/reinforcements, objectives and rewards. Objectives: annihilation, facility income, trap damage, critical hits.
- CostManager and ManagementSettlementManager separate immediate construction/hiring payments from deferred wave income/expenses, with upkeep, debt, interest and repayment. This distinction prevents double accounting.
- Unit condition includes fatigue, injuries, traits, personalities and commands. Commands can be issued during combat and share the power resource; movement/recall have separate restrictions.
- DungeonPowerSystem allows targeted intervention. Morale/policies affect upkeep, rewards, combat and recruitment. VillageConquestSystem connects expedition results to future raid suppression. Core links grant reward bonuses; artifacts provide additional progression.

## Architecture and conventions
- Confirmed: MonoBehaviour scene composition with serialized references, ScriptableObject definitions, typed GameEventChannelSO events, numerous static Current accessors, coroutine sequencing, and ordered save-agent composition.
- Gameplay namespaces follow `Code.<Feature>` and reusable primitives follow `GameLib.Entity[.*]`, with Allman braces, private SerializeField fields, mixed camelCase/_camelCase, and Korean intent comments. Follow the reference-project style: place brief Korean comments directly above non-obvious branches or inline with a field/value, and use XML summaries for reusable public APIs; explain constraints and ownership rather than restating syntax.
- Repository-wide C# formatting and naming rules are in `.editorconfig`; the practical migration guide is `Docs/AI/CodingStyle.md`. Preserve serialized camelCase fields while using `_camelCase` for new runtime-only private state.
- RunSaveSystem writes versioned JSON checkpoints and a backup under persistentDataPath. Save agents capture independent sections. Incomplete restoration blocks subsequent saves to avoid overwriting recoverable state.
- Likely change-sensitive areas: WaveManager, DungeonGraphController, BattleAgent, scene wiring, event ordering during settlement, and restoration order. These are review priorities, not validated defects.

## Testing and tooling
- 26 EditMode C# test files in `Assets/Tests/EditMode/Reflected` cover combat formulas, rules, encounter variety, dialogue, building serialization, asset wiring, intrusion/save and exploitation progress, among others. Source contains 229 [Test] annotations plus parameterized cases (counted 2026-10-14); this is not a discovered test-run count.
- There is no PlayMode test assembly. Do not infer test availability from the stale generated `Defence.PlayMode.Tests.csproj`.
- No builds, tests or Play Mode were run for this analysis; current compilation, runtime behavior, performance and balance remain unverified.
- Unity MCP tools are exposed (console, command execution, captures); manifest contains AI Assistant and com.unity.pipeline. Live connectivity was not tested this turn. Previous context's successful console query is historical, not a current baseline.
- Repository inspection provides settings, scenes, packages and asset evidence without requiring Editor mutation.

## Evidence and constraints
- Inspected: ProjectVersion, manifest, EditorBuildSettings, Player/Graphics/Quality settings, UniversalRP/Renderer2D assets, SampleScene configuration, WaveConfig and its GUID.
- Representative source: DayManager, WaveManager, WaveConfigSO, WaveObjective, WaveExploitationProgress, CostManager, ManagementSettlementManager, GameOverManager, UnitManagementSystem, DungeonPowerSystem, MoralePolicyManager, CoreCohesionSystem, VillageConquestSystem, DungeonGraphController, BattleAgent, AdventurerTrait, UnitConditionState, Store/Inn/Blacksmith, RunSaveSystem, StartMenuController and GameEventChannelSO; character prefab autoDrive fields and test assembly/inventory.
- Analysis is static. Player-facing clarity, difficulty, pacing and actual scene behavior require runtime observation. No gameplay assets or code were changed by onboarding.

<!-- unity-onboarding:generated:end -->

