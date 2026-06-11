using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DungeonExporer.AI
{
    /// <summary>
    /// Renders <c>prompts/cap_personality.jinja2</c> with real Jinja2 (Python). No C# template fallback.
    /// </summary>
    public static class CapPersonalityPromptBuilder
    {
        private const string TemplateFileName = "cap_personality.jinja2";
        private const string RendererScriptName = "render_cap_prompt.py";

        public static string BuildVoicePrompt(
            string displayName,
            string questTitle,
            string questBriefing,
            string worldContext,
            string inventorySummary,
            string memoryBlock,
            bool questActive,
            bool questCompleted,
            int maxSentences = 4)
        {
            return Render(new Dictionary<string, string>
            {
                ["display_name"] = displayName ?? "Cap",
                ["mode"] = "voice",
                ["quest_state"] = ResolveQuestState(questActive, questCompleted),
                ["quest_title"] = questTitle ?? string.Empty,
                ["quest_briefing"] = questBriefing ?? string.Empty,
                ["world_context"] = worldContext ?? string.Empty,
                ["inventory_summary"] = inventorySummary ?? string.Empty,
                ["memory_block"] = memoryBlock ?? string.Empty,
                ["max_sentences"] = maxSentences.ToString()
            });
        }

        public static string BuildReactivePrompt(
            string displayName,
            string questTitle,
            string questBriefing,
            string worldContext,
            string inventorySummary,
            string memoryBlock,
            string playerQuestion,
            bool questActive,
            bool questCompleted,
            int maxSentences = 3)
        {
            return Render(new Dictionary<string, string>
            {
                ["display_name"] = displayName ?? "Cap",
                ["mode"] = "reactive",
                ["quest_state"] = ResolveQuestState(questActive, questCompleted),
                ["quest_title"] = questTitle ?? string.Empty,
                ["quest_briefing"] = questBriefing ?? string.Empty,
                ["world_context"] = worldContext ?? string.Empty,
                ["inventory_summary"] = inventorySummary ?? string.Empty,
                ["memory_block"] = memoryBlock ?? string.Empty,
                ["player_question"] = playerQuestion ?? string.Empty,
                ["max_sentences"] = maxSentences.ToString()
            });
        }

        private static string ResolveQuestState(bool questActive, bool questCompleted)
        {
            if (questCompleted)
                return "completed";
            if (questActive)
                return "active";
            return "considering";
        }

        private static string Render(IReadOnlyDictionary<string, string> context)
        {
            if (!TryResolveTemplatePath(out string templatePath))
            {
                Debug.LogError(
                    "CapPersonalityPromptBuilder: missing cap_personality.jinja2. " +
                    "Expected prompts/cap_personality.jinja2 or StreamingAssets/Prompts/cap_personality.jinja2.");
                return string.Empty;
            }

            if (!TryResolveRendererScriptPath(out string scriptPath))
            {
                Debug.LogError(
                    "CapPersonalityPromptBuilder: missing render_cap_prompt.py under prompts/.");
                return string.Empty;
            }

            string contextJsonPath = Path.Combine(
                Application.temporaryCachePath,
                "cap_prompt_context_" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                File.WriteAllText(contextJsonPath, JsonConvert.SerializeObject(context), Encoding.UTF8);
                if (!TryRunJinjaRenderer(scriptPath, templatePath, contextJsonPath, out string rendered, out string error))
                {
                    Debug.LogError("CapPersonalityPromptBuilder: " + error);
                    return string.Empty;
                }

                return rendered;
            }
            finally
            {
                TryDeleteFile(contextJsonPath);
            }
        }

        private static bool TryRunJinjaRenderer(
            string scriptPath,
            string templatePath,
            string contextJsonPath,
            out string rendered,
            out string error)
        {
            rendered = string.Empty;
            error = string.Empty;

            foreach (string pythonExe in new[] { "python", "python3" })
            {
                if (!TryRunProcess(pythonExe, scriptPath, templatePath, contextJsonPath, out rendered, out error))
                    continue;

                return true;
            }

            return false;
        }

        private static bool TryRunProcess(
            string pythonExe,
            string scriptPath,
            string templatePath,
            string contextJsonPath,
            out string stdout,
            out string error)
        {
            stdout = string.Empty;
            error = string.Empty;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = Quote(scriptPath) + " " + Quote(templatePath) + " " + Quote(contextJsonPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using Process process = Process.Start(psi);
                if (process == null)
                    return false;

                stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(15000))
                {
                    try { process.Kill(); } catch { /* ignored */ }
                    error = "Jinja2 render timed out.";
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    error = string.IsNullOrWhiteSpace(stderr)
                        ? "Jinja2 render failed (exit " + process.ExitCode + "). Is jinja2 installed? pip install jinja2"
                        : stderr.Trim();
                    return false;
                }

                return !string.IsNullOrWhiteSpace(stdout);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryResolveTemplatePath(out string path)
        {
            if (TryRepoPromptsFile(TemplateFileName, out path))
                return true;

            string streaming = Path.Combine(Application.streamingAssetsPath, "Prompts", TemplateFileName);
            if (File.Exists(streaming))
            {
                path = streaming;
                return true;
            }

            path = null;
            return false;
        }

        private static bool TryResolveRendererScriptPath(out string path)
        {
            if (TryRepoPromptsFile(RendererScriptName, out path))
                return true;

            string streaming = Path.Combine(Application.streamingAssetsPath, "Prompts", RendererScriptName);
            if (File.Exists(streaming))
            {
                path = streaming;
                return true;
            }

            path = null;
            return false;
        }

        private static bool TryRepoPromptsFile(string fileName, out string path)
        {
            string repoPrompts = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "prompts", fileName));
            if (File.Exists(repoPrompts))
            {
                path = repoPrompts;
                return true;
            }

            path = null;
            return false;
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
