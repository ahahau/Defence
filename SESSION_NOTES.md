# Session Notes

This file is maintained automatically by the Unity editor helper in `Assets/Editor/SessionNotesAutoSave.cs`.

## Generated Snapshot

- Updated: `2026-03-23 23:46:16`
- Active scene: `Assets/00.Scenes/SampleScene.unity`
- Scene dirty: `False`

### Codex Startup Handoff
- On the next session, read the prompt sections in `Manual Notes` before making project changes.
- Prompt entries are preserved automatically when the scene is saved or the Unity editor quits.
- Latest saved prompt:
  - Summarize the latest saved prompt from this file before editing code.
  - Continue from the listed focus areas after checking console or scene state if needed.
- Most recent prompt history entry: `2026-03-11 14:30:00` Added a persistent startup-prompt workflow so Codex reads this section first on the next session.

### Runtime Objects
- GameManager root present: `True`
- GameCanvas present: `False`
- EventSystem present: `True`
- UIManager present: `True`
- InputManager present: `True`
- BuildManager present: `True`
- WaveManager present: `True`
- EnemySpawnerManager present: `True`
- UIManager available building count: `2`

### Assets
- Wave channel asset present: `True`
- Obstacle building data present: `True`
- NormalBuilding data present: `True`

### Current Setup
- Build flow: `InputManager -> UIManager -> MainPanel -> BuildManager`
- Wave flow: `WaveManager <-> WaveEventChannel.asset <-> EnemySpawnerManager`
- Scene bootstrap helper: `Tools/Setup Game Scene UI`
- Session notes manual update: `Tools/Session Notes/Update Now`
- Auto update trigger: Unity Editor quit

### Recent Important Files
- `Assets/08.SO/Events/WaveEventChannel.asset` : `2026-03-11 10:17:43`
- `Assets/08.SO/Buildings/Obstacle.asset` : `2026-03-19 12:58:12`
- `Assets/08.SO/Buildings/NormalBuilding.asset` : `2026-03-19 12:58:12`
- `Assets/01.Code/UI/UIHeader.cs` : `2026-03-19 10:20:28`
- `Assets/01.Code/UI/MainPanel.cs` : `2026-03-19 12:36:17`
- `Assets/01.Code/UI/BuildingOptionView.cs` : `2026-03-19 12:00:21`
- `Assets/01.Code/Manager/WaveManager.cs` : `2026-03-19 10:34:28`
- `Assets/01.Code/Manager/UIManager.cs` : `2026-03-22 23:44:00`
- `Assets/01.Code/Manager/InputManager.cs` : `2026-03-19 10:34:28`
- `Assets/01.Code/Manager/GameManager.cs` : `2026-03-19 10:34:28`
- `Assets/01.Code/Manager/EnemySpawnerManager.cs` : `2026-03-22 23:39:25`
- `Assets/01.Code/Manager/BuildManager.cs` : `2026-03-19 10:34:28`
- `Assets/01.Code/Cameras/CameraInputSO.cs` : missing
- `Assets/00.Scenes/SampleScene.unity` : `2026-03-22 23:44:00`

### Known Focus Areas
- BuildPanel screen placement and click behavior
- Input routing between empty ground clicks and object clicks
- Scene bootstrap automation in `SetupGameSceneUI.cs`
- Session persistence via `SESSION_NOTES.md`

### Suggested Next Steps
- Verify BuildPanel placement in Play Mode after latest input fix
- Add clearer feedback for build failure or blocked tiles
- Improve slot visuals and selected state polish
- Optionally move wave start request to channel-based UI flow

## Manual Notes

### Working Rules
- Add durable project notes here.
- This section is preserved when the automatic snapshot updates.
- Interpret user requests with intent-first handling; treat "내 의도를 파악해서 해줘" as an always-on standing instruction for future sessions.

### Recent Decisions
- Persist the standing instruction to infer and act on the user's intent, equivalent to appending "내 의도를 파악해서 해줘" to requests.

### Open Issues
-

### Next Session Prompt
- Summarize the latest saved prompt from this file before editing code.
- Continue from the listed focus areas after checking console or scene state if needed.

### Prompt History
- `2026-03-11 14:30:00` Added a persistent startup-prompt workflow so Codex reads this section first on the next session.
- `2026-03-18 11:xx:xx` Added a persistent instruction to handle future requests intent-first, as if "내 의도를 파악해서 해줘" were appended.
