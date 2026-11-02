using System;
using System.Collections.Generic;
using System.IO;
using DMScripts.DebugTools;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DMScripts.Editor
{
    /// <summary>
    /// Tools > Level Debug menu, plus Unity CLI entry points:
    ///   -executeMethod DMScripts.Editor.LevelDebugBuildTools.BuildWithDebugTool
    ///   -executeMethod DMScripts.Editor.LevelDebugBuildTools.BuildWithoutDebugTool
    /// Optional CLI argument: -buildOutput "path"
    /// </summary>
    public static class LevelDebugBuildTools
    {
        public const string DebugDefine = "LEVEL_DEBUG_TOOL";
        private const string MenuRoot = "Tools/Level Debug/";

        [MenuItem(MenuRoot + "Build With Debug Tool", false, 1)]
        public static void BuildWithDebugTool()
        {
            Build(true);
        }

        [MenuItem(MenuRoot + "Build Without Debug Tool", false, 2)]
        public static void BuildWithoutDebugTool()
        {
            Build(false);
        }

        [MenuItem(MenuRoot + "Open Custom Levels Folder", false, 20)]
        public static void OpenCustomLevelsFolder()
        {
            string folder = LevelDebugLoader.CustomFolderPath;
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem(MenuRoot + "Export Level To JSON...", false, 21)]
        public static void OpenExportWindow()
        {
            LevelDebugExportWindow window = EditorWindow.GetWindow<LevelDebugExportWindow>(true, "Export Level To JSON");
            window.minSize = new Vector2(360f, 120f);
            window.Show();
        }

        public static bool ExportLevel(int gid, string path, out string error)
        {
            if (!LevelDebugLoader.LoadTablesForEditor(out error))
            {
                return false;
            }
            string json = LevelDebugLoader.ExportLevelJson(gid);
            if (json == null)
            {
                error = "Level " + gid + " was not found in LevelData.txt / MapData.txt.";
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            error = string.Empty;
            return true;
        }

        private static void Build(bool withDebugTool)
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in File > Build Profiles (Scene List).");
                return;
            }

            string folder = GetArgument("-buildOutput");
            if (string.IsNullOrEmpty(folder))
            {
                folder = Path.Combine("Builds", target.ToString(), withDebugTool ? "WithDebug" : "Release");
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = GetLocationPath(folder, target),
                options = BuildOptions.None,
                extraScriptingDefines = withDebugTool ? new[] { DebugDefine } : new string[0]
            };

            Debug.Log("[LevelDebug] Building " + target + (withDebugTool ? " WITH" : " WITHOUT") + " debug tool -> " + options.locationPathName);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[LevelDebug] Build succeeded: " + options.locationPathName);
                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(options.locationPathName);
                }
            }
            else
            {
                Fail("Build " + report.summary.result + " with " + report.summary.totalErrors + " error(s). See the Console.");
            }
        }

        private static string[] GetEnabledScenes()
        {
            List<string> list = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    list.Add(scene.path);
                }
            }
            return list.ToArray();
        }

        private static string GetLocationPath(string folder, BuildTarget target)
        {
            string name = string.IsNullOrEmpty(PlayerSettings.productName) ? "Game" : PlayerSettings.productName;
            switch (target)
            {
                case BuildTarget.Android:
                    return Path.Combine(folder, name + (EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk"));
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(folder, name + ".exe");
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(folder, name + ".app");
                default:
                    return folder; // iOS, WebGL and others build into a folder
            }
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static void Fail(string message)
        {
            Debug.LogError("[LevelDebug] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorUtility.DisplayDialog("Level Debug", message, "OK");
            }
        }
    }

    /// <summary>Small window: type a level number, export it as an editable custom level JSON.</summary>
    public class LevelDebugExportWindow : EditorWindow
    {
        private int levelNumber = 1;
        private string message = string.Empty;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Saves the level to:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(LevelDebugLoader.DefaultCustomFilePath, GUILayout.Height(18f));
            levelNumber = Mathf.Max(1, EditorGUILayout.IntField("Level number", levelNumber));

            if (GUILayout.Button("Export"))
            {
                string error;
                if (LevelDebugBuildTools.ExportLevel(levelNumber, LevelDebugLoader.DefaultCustomFilePath, out error))
                {
                    message = "Exported level " + levelNumber + ". Edit the file, then use \"Load Custom JSON\" in Play mode.";
                }
                else
                {
                    message = error;
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
        }
    }
}
