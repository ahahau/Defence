# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:\Fork\Defence`
- Product: `Defence`, a 2D dungeon-management and automated-combat game. The core loop is dungeon expansion, unit/building placement, adventurer waves, settlement/rewards, and further expansion.
- Last analyzed: 2026-09-01 (Asia/Seoul)
- Last analyzed commit: `258a4c931b63afb6f712470f87edd12d4b289373` (`test: make the intrusion and save tests actually run`)
- Snapshot note: the working tree was dirty at analysis time (58 tracked changes and 38 untracked entries). Findings describe the working tree, not only the commit above.

## Confirmed Environment

- Unity version: **6000.4.6f1**, revision `0b051c2e5d54` (Unity 6.4).
- Render pipeline: **URP 17.4.0 with a 2D Renderer**. Every quality level references `Assets/Settings/UniversalRP.asset`, which uses `Renderer2D.asset`.
- Input system: **new Input System only** (`com.unity.inputsystem` 1.19.0, `activeInputHandler: 1`, generated `Controls.cs`).
- Primary target evidence: a Windows standalone Build Profile exists. Android/iOS settings are present in generic Player Settings, but supported release targets are not documented.
- Networking: no first-party networking API usage was found. `Multiplayer Center` is installed as tooling only; this is not currently evidenced as a multiplayer game.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 17.4.0, Renderer2D | Confirmed | `Packages/manifest.json`, `ProjectSettings/QualitySettings.asset`, `Assets/Settings/UniversalRP.asset` |
| Input | Input System 1.19.0 and generated controls wrapper | Confirmed | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, `Assets/01.Code/Core/Input/Controls.cs` |
| Combat AI | Unity Behavior 1.0.16; graphs decide behavior and `BattleAgent` executes it | Confirmed | `Packages/manifest.json`, `Assets/01.Code/BT/BattleAgent.cs`, `Assets/05.Graphs/` |
| Feedback/animation | DOTween Pro and More Mountains Feel/MMFeedbacks | Confirmed | `Assets/Plugins/Demigiant/`, `Assets/Feel/`, first-party combat/UI usage |
| UI | uGUI + TextMesh Pro; scene-bound view/controller classes | Confirmed | `Packages/manifest.json`, `Assets/01.Code/UI/`, build scenes |
| Persistence | Versioned JSON checkpoints, backup file, registry of `ISaveable` agents | Confirmed | `Assets/01.Code/Persistence/` |
| Tests | Unity Test Framework 1.6.0 and NUnit EditMode tests | Confirmed | package manifest and `Assets/Tests/EditMode/Reflected/` |
| Editor tooling | Unity AI Assistant and experimental `com.unity.pipeline` MCP bridge | Confirmed | package manifest/lock, successful MCP console query |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/00.Scenes/` | Start, main gameplay, and development/test scenes | Confirmed | Four `.unity` files; two enabled in Build Settings |
| `Assets/01.Code/` | 244 first-party C# files grouped by feature/system | Confirmed | source inventory |
| `Assets/02.Art/` | First-party visual assets | Likely | folder and asset names |
| `Assets/03.SO/` | ScriptableObject game data: units, enemies, buildings, waves, policies, skills, stats | Confirmed | 229 non-meta assets and corresponding SO types |
| `Assets/04.Prefab/` | Gameplay, UI, character, building, and map prefabs | Confirmed | 54 non-meta assets |
| `Assets/05.Graphs/` | Unity Behavior graph assets | Confirmed | graph assets and `Unity.Behavior` code usage |
| `Assets/Settings/` | URP/Renderer2D and Windows Build Profile | Confirmed | settings assets |
| `Assets/Tests/` | Reflection-oriented EditMode tests | Confirmed | test assembly and six test files |
| `Assets/Feel/`, `Assets/Plugins/`, `Assets/csiimnida/`, `Assets/vHierarchy/` | Imported/vendor runtime and editor tooling | Confirmed | package/vendor assemblies and source |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Assembly-CSharp` (implicit) | All 244 first-party runtime scripts | Unity packages and auto-referenced vendor assemblies | No first-party runtime `.asmdef`; most production code compiles as one monolith. |
| `Assembly-CSharp-Editor` (implicit) | `Assets/01.Code/Editor/` tools and inspectors | Production code, UnityEditor | Editor separation relies on Unity's reserved folder behavior. |
| `Defence.EditMode.Tests` | 58 NUnit EditMode tests across six files | Test runners and `nunit.framework.dll` | Does not reference a first-party runtime assembly; tests under `Reflected` reach production code via reflection. |
| Vendor assemblies | More Mountains, Nice Vibrations, SoundManager, vHierarchy | Package-specific | Third-party boundaries exist but do not partition first-party gameplay code. |

The principal structural risk is the missing first-party runtime assembly boundary: feature dependencies are unchecked, compilation scope is broad, and tests lack compile-time references to production types.

## Scenes And Startup Flow

- Enabled build scenes, in order:
  1. `Assets/00.Scenes/Start.unity`
  2. `Assets/00.Scenes/SampleScene.unity`
- `Start.unity` hosts the start/settings UI. `StartMenuController` loads `SampleScene` by name.
- In `SampleScene`, `DungeonGraphController.Awake()` creates the initial graph and locked nodes. After one frame its `Start()` asks `RunSaveSystem` to restore a checkpoint, allowing other managers/UI to initialize first.
- Day/wave flow is event-driven: `DayManager` advances the day, `WaveManager` executes spawning/combat/reward flow, and a wave-end event returns the game to standby and triggers saving.
- Development-only scenes excluded from builds: `BattleTest.unity` and `DialogueTestScene.unity` (plus imported demo scenes elsewhere under `Assets`).
- `SampleScene.unity` is a large composition root: about 22,310 serialized lines and 176 named objects at analysis time.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene composition | MonoBehaviours and serialized references wire most systems in `SampleScene` | Confirmed | scene YAML and serialized fields across managers/views |
| ScriptableObject data | Units, enemies, buildings, policies, skills, stats, waves, dialogue, and artifacts are data-driven | Confirmed | 40 `CreateAssetMenu` declarations and `Assets/03.SO/` |
| Typed event bus | `GameEventChannelSO` dispatches typed `GameEvent` objects; feature-specific event classes decouple many UI/gameplay reactions | Confirmed | `Core/GameEventChannelSO.cs`, `Events/`, 42 referencing files |
| Service locator/singletons | Numerous managers and some views expose static `Current` accessors | Confirmed | 21 matching declarations/usages across first-party code |
| Entity/module composition | `ModuleOwner` discovers child `IModule`s; `Entity` exposes common sensing/stats/health signals | Confirmed | `Core/Modules/` |
| Behavior trees | Unity Behavior chooses combat actions; `BattleAgent` is the execution adapter | Confirmed | `BT/`, `Assets/05.Graphs/` |
| Save-agent registry | Save/restore delegates to ordered `ISaveable` agents and writes versioned JSON with a backup | Confirmed | `Persistence/` |
| Async model | Frame/timing work uses coroutines; no task/UniTask convention is present | Confirmed | coroutine usage and no async framework usage |

Important maintainability hotspots are `WaveManager` (about 956 lines), `DungeonGraphController` (about 1,065 lines), the large gameplay scene, and broad use of static `Current` access. These make initialization order and cross-feature changes harder to reason about even though event channels reduce some direct coupling.

## Coding Conventions

- Namespaces: feature-based `_01.Code.<Feature>` namespaces; persistence agents use `_01.Code.Persistence.Agents`.
- Layout: Allman braces and generally one public type per file.
- Serialized fields: private `[SerializeField]`; field naming is mixed between `camelCase` and `_camelCase`.
- Documentation: Korean XML summaries and intent-focused comments are common in newer/core code, but not universal.
- Nullability: nullable reference types are not enabled; defensive Unity-null checks are common.
- Async: coroutines for sequencing and timing; no `async`/`UniTask` usage found.
- Build configuration suppresses CS0618 and treats MSB3277 as a message via `Directory.Build.targets`.

## Testing And Validation

- EditMode: **58 `[Test]` cases** across combat formulas, game rules, encounter variety, dialogue, serialization, intrusion, and saves.
- PlayMode: no PlayMode test assembly or `[UnityTest]` cases found.
- CI: no repository CI workflow or automated Unity test command was found.
- Current baseline: no tests/build were run during onboarding. The connected Editor console reported **0 errors and 2 warnings** (automation-mode warning and Unity AI account API timeout); this is not equivalent to a clean build/test run.
- Historical evidence only: `Docs/작업정리.txt` records a successful `dotnet build Defence.sln --no-restore` on 2026-07-15 at commit `939b81ba`, not for the current working tree.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| `unity.connection.status` | available | MCP console request succeeded against the open Editor |
| `unity.editor.version` | available from repository; dedicated API unverified | `ProjectVersion.txt` |
| `unity.console.read` | available | MCP returned current warnings/errors |
| `unity.scene.list` | available from serialized settings; dedicated API unverified | `EditorBuildSettings.asset` |
| `unity.scene.inspect` | unverified | no read-only hierarchy API exposed/used |
| `unity.buildsettings.read` | available from repository; dedicated API unverified | `EditorBuildSettings.asset`, Build Profile asset |
| `unity.gameobject.inspect` | unverified | no dedicated inspection call used |
| `unity.asset.search` | available through repository search; dedicated API unverified | filesystem/`rg` |
| `unity.package.read` | available through repository files; dedicated API unverified | package manifest/lock |
| `unity.tests.list` | unverified | test files inspected from repository |
| `unity.tests.run` | unverified | not invoked during onboarding |
| `unity.playmode.read` | unverified | not invoked; Play Mode was not entered |
| `unity.profiler.read` | unverified | not exposed/used |

## Important Constraints

- Preserve the user's dirty working tree; many gameplay, prefab, ScriptableObject, scene, and test changes are uncommitted.
- Treat `Library/`, `Temp/`, `Logs/`, and `obj/` as generated.
- Scene and prefab wiring is architecturally significant; script changes often require validation of serialized references.
- Save-agent order is a dependency contract: world/layout restoration must precede dependent state such as units/buildings.
- Do not infer multiplayer support from the Multiplayer Center package.

## Unknowns And Confidence

- Supported shipping platforms and release/build procedure are undocumented; only a Windows profile is confirmed.
- Current compile, EditMode test, player-build, and runtime baselines remain unknown because onboarding intentionally did not trigger builds/tests/Play Mode.
- Performance characteristics are unknown; no profiler capture was performed.
- The exact intended long-term assembly/layer boundaries are undocumented.
- The large dirty change set means architecture and test counts may change before the next commit.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `Assets/Settings/UniversalRP.asset`, `Assets/Settings/Renderer2D.asset`, `Assets/Settings/Build Profiles/Windows.asset`
- `Assets/00.Scenes/Start.unity`, `Assets/00.Scenes/SampleScene.unity`
- Representative code under `Assets/01.Code/Core/`, `Manager/`, `MapCreateSystem/`, `BT/`, `Persistence/`, `UI/`, and `Events/`
- `Assets/Tests/EditMode/Reflected/Defence.EditMode.Tests.asmdef` and all six EditMode test files
- `Docs/작업정리.txt`, `Docs/발표대본.md`, `Directory.Build.targets`
- Git status, current commit, source/asset inventories, and current Unity Editor console

<!-- unity-onboarding:generated:end -->
