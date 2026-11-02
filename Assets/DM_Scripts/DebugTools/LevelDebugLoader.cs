#if UNITY_EDITOR || DEVELOPMENT_BUILD || LEVEL_DEBUG_TOOL
using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DMScripts.DebugTools
{
    /// <summary>
    /// Custom level file: the same fields as LevelData.txt ("game") and MapData.txt ("map").
    /// </summary>
    [Serializable]
    public class CustomLevelFile
    {
        public PacketGameLevelData game;
        public PacketGameMapData[] map;
    }

    /// <summary>
    /// Debug-only level loading. Changes level data in memory only; shipped files are never written.
    /// </summary>
    public static class LevelDebugLoader
    {
        public const string CustomFolderName = "DebugLevels";
        public const string DefaultCustomFileName = "custom_level.json";

        public static string CustomFolderPath => Path.Combine(Application.persistentDataPath, CustomFolderName);

        public static string DefaultCustomFilePath => Path.Combine(CustomFolderPath, DefaultCustomFileName);

        public static bool IsLevelDataLoaded => MapData.LevelCount > 0;

        public static bool LevelExists(int gid)
        {
            return MapData.dicServerGameData.ContainsKey(gid) && MapData.dicArrayServerMapData.ContainsKey(gid);
        }

        /// <summary>Lobby or Game scene, and no scene change running.</summary>
        public static bool CanStartLevelNow(out string reason)
        {
            SceneControlManager scm = MonoSingleton<SceneControlManager>.Instance;
            if (scm == null)
            {
                reason = "Scene manager not ready yet.";
                return false;
            }
            if (scm.IsLoadingScene)
            {
                reason = "A scene is loading, wait a moment.";
                return false;
            }
            if (scm.CurrentSceneType != SceneType.Lobby && scm.CurrentSceneType != SceneType.Game)
            {
                reason = "Wait until the Lobby screen is shown.";
                return false;
            }
            if (!IsLevelDataLoaded)
            {
                reason = "Level data is not loaded yet.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Parses a custom level JSON and adds/replaces it in the in-memory level tables.
        /// </summary>
        public static bool TryInjectCustomLevel(string json, out int gid, out string error)
        {
            gid = 0;
            if (string.IsNullOrEmpty(json))
            {
                error = "The JSON file is empty.";
                return false;
            }

            CustomLevelFile file;
            try
            {
                file = JsonConvert.DeserializeObject<CustomLevelFile>(json);
            }
            catch (Exception ex)
            {
                error = "JSON error: " + ex.Message;
                return false;
            }

            if (file == null || file.game == null)
            {
                error = "Missing \"game\" section.";
                return false;
            }
            if (file.map == null || file.map.Length == 0)
            {
                error = "Missing \"map\" section (at least one board).";
                return false;
            }
            if (file.game.gid <= 0)
            {
                error = "\"game.gid\" must be 1 or higher.";
                return false;
            }

            gid = file.game.gid;
            file.game.map_count = file.map.Length;
            PacketGameMapData[] boards = new PacketGameMapData[file.map.Length];
            for (int i = 0; i < file.map.Length; i++)
            {
                PacketGameMapData board = file.map[i];
                if (board == null || string.IsNullOrEmpty(board.board_data))
                {
                    error = "Board " + (i + 1) + " has no \"board_data\".";
                    return false;
                }
                board.gid = gid;
                board.mid = i + 1;
                boards[i] = board;
            }

            MapData.dicServerGameData[gid] = file.game;
            MapData.dicArrayServerMapData[gid] = boards;
            error = string.Empty;
            return true;
        }

        public static bool TryInjectCustomLevelFromFile(string path, out int gid, out string error)
        {
            gid = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "File not found: " + path;
                return false;
            }
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                error = "Cannot read file: " + ex.Message;
                return false;
            }
            return TryInjectCustomLevel(json, out gid, out error);
        }

        /// <summary>
        /// Starts a level like the Lobby play button. From the Game scene it goes back to the Lobby first,
        /// because SceneControlManager ignores a load of the scene that is already open.
        /// </summary>
        public static IEnumerator StartLevel(int gid, Action<string> onStatus)
        {
            if (!LevelExists(gid))
            {
                Report(onStatus, "Level " + gid + " does not exist (levels loaded: " + MapData.LevelCount + ").", true);
                yield break;
            }

            SceneControlManager scm = MonoSingleton<SceneControlManager>.Instance;
            if (scm.CurrentSceneType == SceneType.Game)
            {
                Report(onStatus, "Returning to Lobby...", false);
                scm.LoadScene(SceneType.Lobby, SceneChangeEffect.Color);
                yield return null;
                while (scm.IsLoadingScene || scm.CurrentSceneType != SceneType.Lobby)
                {
                    yield return null;
                }
                yield return null;
            }

            PlayerDataManager player = MonoSingleton<PlayerDataManager>.Instance;
            player.lastPlayedLevel = gid;
            MapData.main = new MapData(gid);
            scm.LoadScene(SceneType.Game, SceneChangeEffect.Color);
            GameMain.CompleteGameStart();
            Report(onStatus, "Loading level " + gid + ".", false);
        }

        /// <summary>Builds the custom-level JSON for an existing level (used by the Editor export).</summary>
        public static string ExportLevelJson(int gid)
        {
            if (!LevelExists(gid))
            {
                return null;
            }
            CustomLevelFile file = new CustomLevelFile
            {
                game = MapData.dicServerGameData[gid],
                map = MapData.dicArrayServerMapData[gid]
            };
            return JsonConvert.SerializeObject(file, Formatting.Indented);
        }

        /// <summary>
        /// Loads LevelData.txt and MapData.txt into the in-memory tables without any game manager.
        /// Used in Edit Mode (Editor export). Accepts both a plain array and {"game":[...]} / {"map":[...]}.
        /// </summary>
        public static bool LoadTablesForEditor(out string error)
        {
            try
            {
                PacketGameLevelData[] levels = ParseTable<PacketGameLevelData>(
                    LocalDataManager.GetFileContents(LocalDataManager.FILE_NAME_TABLE_LEVEL_DATA), "game");
                PacketGameMapData[] maps = ParseTable<PacketGameMapData>(
                    LocalDataManager.GetFileContents(LocalDataManager.FILE_NAME_TABLE_MAP_DATA), "map");
                if (levels == null || maps == null)
                {
                    error = "LevelData.txt or MapData.txt could not be read.";
                    return false;
                }

                MapData.dicServerGameData.Clear();
                for (int i = 0; i < levels.Length; i++)
                {
                    if (levels[i] != null && !MapData.dicServerGameData.ContainsKey(levels[i].gid))
                    {
                        MapData.dicServerGameData.Add(levels[i].gid, levels[i]);
                    }
                }
                MapData.ReceiveServerMapData(maps);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static T[] ParseTable<T>(string contents, string wrapperKey)
        {
            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }
            JToken root = JToken.Parse(contents);
            if (root.Type == JTokenType.Object && root[wrapperKey] != null)
            {
                root = root[wrapperKey];
            }
            return root.ToObject<T[]>();
        }

        private static void Report(Action<string> onStatus, string message, bool isError)
        {
            if (isError)
            {
                Debug.LogWarning("[LevelDebug] " + message);
            }
            else
            {
                Debug.Log("[LevelDebug] " + message);
            }
            onStatus?.Invoke(message);
        }
    }
}
#endif
