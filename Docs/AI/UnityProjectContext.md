# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project summary
- Root: `C:\Fork\Defence`; analyzed 2026-09-07 (Asia/Seoul).
- Commit: `b8222be06d518a4504fdd2fd6113a1587af44b56`; findings describe the dirty working tree, including existing gameplay edits and deleted tests. Preserve these changes.
- Confirmed: 2D dungeon management and automated combat. Prepare a node-based dungeon, hire/deploy units and facilities, start adventurer waves, intervene with commands/powers, settle income/upkeep, then expand.
- Distinctive tradeoff: Store increases enemy attack, Inn heals enemies, and Blacksmith increases enemy attack/defense in exchange for facility income. Adventurer traits affect spending, fear, retreat and support healing.
- Current scene starts with 200 gold and a 300 debt limit. The scene-linked WaveConfig specifies bosses every 9 days and the final day at 20 (boss days 9, 12, 18, 20; day 12 is the explicit Greed Knight boss entry). Do not confuse these serialized values with C# defaults (100 starting gold, final day 50).
- Final-wave completion with a defeated boss triggers victory; main-unit defeat or bankruptcy triggers game over.

## Environment and frameworks
- Confirmed Unity 6000.4.6f1, revision 0b051c2e5d54.
- URP 17.4.0, Renderer2D: all quality levels reference UniversalRP.asset, which references Renderer2D.asset. Global GraphicsSettings pipeline is null, but quality overrides establish URP.
- New Input System 1.19.0 only (`activeInputHandler: 1`).
- Unity Behavior 1.0.16; Behavior graph actions call BattleAgent. Character prefabs inspected serialize `autoDrive: 0`; code also supports an automatic fallback driver.
- uGUI/TextMesh Pro, DOTween and Feel/MMFeedbacks are present. Test Framework 1.6.0.
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
- Eight current EditMode C# test files cover combat formulas, rules, encounter variety, dialogue, building serialization, asset wiring, intrusion/save and exploitation progress. Source contains 66 [Test] annotations plus parameterized cases; this is not a discovered test-run count.
- PlayMode test assembly and smoke runner are deleted in the current working tree. Do not infer test availability from stale generated csproj files.
- No builds, tests or Play Mode were run for this analysis; current compilation, runtime behavior, performance and balance remain unverified.
- Unity MCP tools are exposed (console, command execution, captures); manifest contains AI Assistant and com.unity.pipeline. Live connectivity was not tested this turn. Previous context's successful console query is historical, not a current baseline.
- Repository inspection provides settings, scenes, packages and asset evidence without requiring Editor mutation.

## Evidence and constraints
- Inspected: ProjectVersion, manifest, EditorBuildSettings, Player/Graphics/Quality settings, UniversalRP/Renderer2D assets, SampleScene configuration, WaveConfig and its GUID.
- Representative source: DayManager, WaveManager, WaveConfigSO, WaveObjective, WaveExploitationProgress, CostManager, ManagementSettlementManager, GameOverManager, UnitManagementSystem, DungeonPowerSystem, MoralePolicyManager, CoreCohesionSystem, VillageConquestSystem, DungeonGraphController, BattleAgent, AdventurerTrait, UnitConditionState, Store/Inn/Blacksmith, RunSaveSystem, StartMenuController and GameEventChannelSO; character prefab autoDrive fields and test assembly/inventory.
- Analysis is static. Player-facing clarity, difficulty, pacing and actual scene behavior require runtime observation. No gameplay assets or code were changed by onboarding.

<!-- unity-onboarding:generated:end -->

