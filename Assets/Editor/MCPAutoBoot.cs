using System.Diagnostics;
using System.Linq;
using UnityEditor;

namespace Editor
{
    [InitializeOnLoad]
    public class MCPAutoBoot
    {
        static bool initialized;

        static MCPAutoBoot()
        {
            EditorApplication.delayCall += Boot;
        }

        static void Boot()
        {
            if (initialized) return;
            initialized = true;

            StartMCPServer();
            OpenMCPWindow();
            StartCodex();
        }

        static void StartMCPServer()
        {
            // 이미 python MCP 서버 실행 중인지 확인
            var running = Process.GetProcessesByName("python")
                .Any(p => p.MainWindowTitle.ToLower().Contains("mcp"));

            if (running)
            {
                UnityEngine.Debug.Log("MCP server already running.");
                return;
            }

            string serverPath = @"C:\Fork\Defence\Assets\MCPForUnity\Server";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c start python -m uv run python src/main.py --transport http --http-port 8080",
                WorkingDirectory = serverPath,
                UseShellExecute = true
            };

            Process.Start(psi);

            UnityEngine.Debug.Log("MCP server started automatically.");
        }

        static void StartCodex()
        {
            // Codex 이미 실행 중인지 확인
            var running = Process.GetProcessesByName("node")
                .Any(p => p.ProcessName.ToLower().Contains("node"));

            if (running)
            {
                UnityEngine.Debug.Log("Codex already running.");
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c start powershell -NoExit -Command \"cd C:\\Fork\\Defence; codex app\"",
                UseShellExecute = true
            };

            Process.Start(psi);

            UnityEngine.Debug.Log("Codex started.");
        }

        static void OpenMCPWindow()
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in assemblies)
            {
                var type = asm.GetType("MCPForUnity.Editor.MCPWindow");

                if (type != null)
                {
                    EditorWindow.GetWindow(type);
                    UnityEngine.Debug.Log("MCP window opened.");
                    return;
                }
            }

            UnityEngine.Debug.LogWarning("MCP Window not found.");
        }
    }
}