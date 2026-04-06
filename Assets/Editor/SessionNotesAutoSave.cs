using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _01.Code.Buildings;
using _01.Code.Manager;
using _01.Code.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SessionNotesAutoSave
{
    private const string NotesFileName = "SESSION_NOTES.md";
    private const string GeneratedHeader = "## Generated Snapshot";
    private const string ManualHeader = "## Manual Notes";
    private const string NextSessionPromptHeader = "### Next Session Prompt";
    private const string PromptHistoryHeader = "### Prompt History";

    private static readonly string[] ImportantProjectFiles =
    {
        "Assets/00.Scenes/SampleScene.unity",
        "Assets/01.Code/Manager/GameManager.cs",
        "Assets/01.Code/Manager/InputManager.cs",
        "Assets/01.Code/Manager/BuildManager.cs",
        "Assets/01.Code/Manager/WaveManager.cs",
        "Assets/01.Code/Manager/EnemySpawnerManager.cs",
        "Assets/01.Code/UI/MainPanel.cs",
        "Assets/01.Code/UI/BuildingOptionView.cs",
        "Assets/01.Code/UI/UIHeader.cs",
        "Assets/01.Code/Cameras/CameraInputSO.cs",
        "Assets/08.SO/Events/WaveEventChannel.asset",
        "Assets/08.SO/Buildings/Obstacle.asset",
        "Assets/08.SO/Buildings/NormalBuilding.asset"
    };

    static SessionNotesAutoSave()
    {
        EditorApplication.quitting -= SaveSnapshot;
        EditorApplication.quitting += SaveSnapshot;
        EditorSceneManager.sceneSaved -= HandleSceneSaved;
        EditorSceneManager.sceneSaved += HandleSceneSaved;
    }

    [MenuItem("Tools/Session Notes/Update Now")]
    public static void SaveSnapshotMenu()
    {
        SaveSnapshot();
        AssetDatabase.Refresh();
        Debug.Log("SESSION_NOTES.md updated.");
    }

    private static void SaveSnapshot()
    {
        try
        {
            string notesPath = GetNotesPath();
            string existing = File.Exists(notesPath) ? File.ReadAllText(notesPath) : string.Empty;
            string manualSection = ExtractManualSection(existing);
            string generatedSection = BuildGeneratedSection(manualSection);

            var content = new StringBuilder();
            content.AppendLine("# Session Notes");
            content.AppendLine();
            content.AppendLine("This file is maintained automatically by the Unity editor helper in `Assets/Editor/SessionNotesAutoSave.cs`.");
            content.AppendLine();
            content.AppendLine(GeneratedHeader);
            content.AppendLine();
            content.Append(generatedSection.TrimEnd());
            content.AppendLine();
            content.AppendLine();
            content.AppendLine(ManualHeader);
            content.AppendLine();
            content.Append(string.IsNullOrWhiteSpace(manualSection)
                ? BuildDefaultManualSection()
                : manualSection.TrimEnd() + "\n");

            File.WriteAllText(notesPath, content.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update {NotesFileName}: {ex}");
        }
    }

    private static void HandleSceneSaved(Scene _)
    {
        SaveSnapshot();
        AssetDatabase.Refresh();
    }

    private static string BuildGeneratedSection(string manualSection)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject gameManagerRoot = GameObject.Find("GameManager");
        GameObject canvas = GameObject.Find("GameCanvas");
        GameObject eventSystem = GameObject.Find("EventSystem");

        InputManager inputManager = gameManagerRoot != null ? gameManagerRoot.GetComponentInChildren<InputManager>(true) : null;
        BuildManager buildManager = gameManagerRoot != null ? gameManagerRoot.GetComponentInChildren<BuildManager>(true) : null;
        WaveManager waveManager = gameManagerRoot != null ? gameManagerRoot.GetComponentInChildren<WaveManager>(true) : null;
        EnemySpawnerManager enemySpawnerManager = gameManagerRoot != null ? gameManagerRoot.GetComponentInChildren<EnemySpawnerManager>(true) : null;

        bool hasWaveChannel = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/08.SO/Events/WaveEventChannel.asset") != null;
        bool hasObstacleData = AssetDatabase.LoadAssetAtPath<UnitDataSO>("Assets/08.SO/Buildings/Obstacle.asset") != null;
        bool hasNormalBuildingData = AssetDatabase.LoadAssetAtPath<UnitDataSO>("Assets/08.SO/Buildings/NormalBuilding.asset") != null;

        int availableBuildingCount = 0;
        int availableUnitCount = 0;
        if (buildManager != null)
        {
            SerializedObject serialized = new SerializedObject(buildManager);
            SerializedProperty availableBuildings = serialized.FindProperty("availableBuildings");
            SerializedProperty availableUnits = serialized.FindProperty("availableUnits");
            availableBuildingCount = availableBuildings != null ? availableBuildings.arraySize : 0;
            availableUnitCount = availableUnits != null ? availableUnits.arraySize : 0;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"- Updated: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine($"- Active scene: `{activeScene.path}`");
        sb.AppendLine($"- Scene dirty: `{activeScene.isDirty}`");
        sb.AppendLine();

        AppendPromptHandoff(sb, manualSection);

        sb.AppendLine("### Runtime Objects");
        sb.AppendLine($"- GameManager root present: `{(gameManagerRoot != null)}`");
        sb.AppendLine($"- GameCanvas present: `{(canvas != null)}`");
        sb.AppendLine($"- EventSystem present: `{(eventSystem != null)}`");
        sb.AppendLine($"- InputManager present: `{(inputManager != null)}`");
        sb.AppendLine($"- BuildManager present: `{(buildManager != null)}`");
        sb.AppendLine($"- WaveManager present: `{(waveManager != null)}`");
        sb.AppendLine($"- EnemySpawnerManager present: `{(enemySpawnerManager != null)}`");
        sb.AppendLine($"- BuildManager available building count: `{availableBuildingCount}`");
        sb.AppendLine($"- BuildManager available unit count: `{availableUnitCount}`");
        sb.AppendLine();

        sb.AppendLine("### Assets");
        sb.AppendLine($"- Wave channel asset present: `{hasWaveChannel}`");
        sb.AppendLine($"- Obstacle building data present: `{hasObstacleData}`");
        sb.AppendLine($"- NormalBuilding data present: `{hasNormalBuildingData}`");
        sb.AppendLine();

        if (canvas != null)
        {
            sb.AppendLine("### Canvas Children");
            foreach (Transform child in canvas.transform)
            {
                sb.AppendLine($"- `{child.name}`");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Current Setup");
        sb.AppendLine("- Build flow: `InputManager -> BuildManager -> TownInteriorScreenUI`");
        sb.AppendLine("- Wave flow: `WaveManager <-> WaveEventChannel.asset <-> EnemySpawnerManager`");
        sb.AppendLine("- Scene bootstrap helper: `missing / update handoff if replaced`");
        sb.AppendLine("- Session notes manual update: `Tools/Session Notes/Update Now`");
        sb.AppendLine("- Auto update trigger: Unity Editor quit");
        sb.AppendLine();

        sb.AppendLine("### Recent Important Files");
        foreach (string line in BuildImportantFileLines())
        {
            sb.AppendLine(line);
        }
        sb.AppendLine();

        sb.AppendLine("### Known Focus Areas");
        sb.AppendLine("- BuildPanel screen placement and click behavior");
        sb.AppendLine("- Input routing between empty ground clicks and object clicks");
        sb.AppendLine("- Scene bootstrap automation in `SetupGameSceneUI.cs`");
        sb.AppendLine("- Session persistence via `SESSION_NOTES.md`");
        sb.AppendLine();

        sb.AppendLine("### Suggested Next Steps");
        sb.AppendLine("- Verify BuildPanel placement in Play Mode after latest input fix");
        sb.AppendLine("- Add clearer feedback for build failure or blocked tiles");
        sb.AppendLine("- Improve slot visuals and selected state polish");
        sb.AppendLine("- Optionally move wave start request to channel-based UI flow");
        return sb.ToString();
    }

    private static void AppendPromptHandoff(StringBuilder sb, string manualSection)
    {
        string nextPrompt = ExtractMarkdownSection(manualSection, NextSessionPromptHeader);
        string promptHistory = ExtractMarkdownSection(manualSection, PromptHistoryHeader);

        sb.AppendLine("### Codex Startup Handoff");
        sb.AppendLine("- On the next session, read the prompt sections in `Manual Notes` before making project changes.");
        sb.AppendLine("- Prompt entries are preserved automatically when the scene is saved or the Unity editor quits.");

        if (!string.IsNullOrWhiteSpace(nextPrompt))
        {
            sb.AppendLine("- Latest saved prompt:");
            foreach (string line in nextPrompt
                         .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                         .Select(static line => line.TrimEnd())
                         .Where(static line => !string.IsNullOrWhiteSpace(line)))
            {
                sb.AppendLine($"  {line}");
            }
        }
        else
        {
            sb.AppendLine("- Latest saved prompt: `None`");
        }

        if (!string.IsNullOrWhiteSpace(promptHistory))
        {
            string latestHistoryLine = promptHistory
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(static line => line.Trim())
                .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line) && line != "-");

            if (!string.IsNullOrWhiteSpace(latestHistoryLine))
            {
                sb.AppendLine($"- Most recent prompt history entry: {latestHistoryLine.TrimStart('-', ' ')}");
            }
        }

        sb.AppendLine();
    }

    private static IEnumerable<string> BuildImportantFileLines()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var lines = new List<string>();

        foreach (string relativePath in ImportantProjectFiles)
        {
            string fullPath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                lines.Add($"- `{relativePath}` : missing");
                continue;
            }

            DateTime writeTime = File.GetLastWriteTime(fullPath);
            lines.Add($"- `{relativePath}` : `{writeTime:yyyy-MM-dd HH:mm:ss}`");
        }

        return lines.OrderByDescending(line => line);
    }

    private static string ExtractManualSection(string existing)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return BuildDefaultManualSection();
        }

        int manualIndex = existing.LastIndexOf(ManualHeader, StringComparison.Ordinal);
        if (manualIndex < 0)
        {
            return BuildDefaultManualSection();
        }

        int manualContentStart = manualIndex + ManualHeader.Length;
        return existing.Substring(manualContentStart).TrimStart('\r', '\n');
    }

    private static string ExtractMarkdownSection(string source, string header)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        int startIndex = source.IndexOf(header, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        int contentStart = startIndex + header.Length;
        int nextHeaderIndex = source.IndexOf("\n### ", contentStart, StringComparison.Ordinal);
        string section = nextHeaderIndex >= 0
            ? source.Substring(contentStart, nextHeaderIndex - contentStart)
            : source.Substring(contentStart);

        return section.Trim('\r', '\n', ' ', '\t');
    }

    private static string BuildDefaultManualSection()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Working Rules");
        sb.AppendLine("- Add durable project notes here.");
        sb.AppendLine("- This section is preserved when the automatic snapshot updates.");
        sb.AppendLine();
        sb.AppendLine("### Recent Decisions");
        sb.AppendLine("-");
        sb.AppendLine();
        sb.AppendLine("### Open Issues");
        sb.AppendLine("-");
        sb.AppendLine();
        sb.AppendLine("### Next Session Prompt");
        sb.AppendLine("- Summarize the latest saved prompt from this file before editing code.");
        sb.AppendLine("- Continue from the listed focus areas after checking console or scene state if needed.");
        sb.AppendLine();
        sb.AppendLine("### Prompt History");
        sb.AppendLine($"- `{DateTime.Now:yyyy-MM-dd HH:mm:ss}` Initial session prompt template created.");
        return sb.ToString();
    }

    private static string GetNotesPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", NotesFileName));
    }
}
