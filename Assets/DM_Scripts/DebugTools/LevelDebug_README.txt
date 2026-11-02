LEVEL DEBUG TOOL
================

Where levels live
- Assets/Resources/dotmob_data_decrypt/LevelData.txt  (one entry per level: gid, game_mode, move_count, star points, collect_data, map_mode, map_count)
- Assets/Resources/dotmob_data_decrypt/MapData.txt    (board layouts: gid, mid = board number, board_data, drop, road, moving, rotation, map_param)
- A copy in Application.persistentDataPath overrides the Resources file.
- new MapData(gid) builds a level from these two tables.

Included in: the Unity Editor, Development Builds, and builds made with "Build With Debug Tool".
Removed from: normal release builds ("Build Without Debug Tool").

TEST A LEVEL (Editor)
1. File > Open Scene > Assets/DM_Scenes/Loading.unity
2. Press Play.
3. Wait for the Lobby screen.
4. Click "LVL" (top-left of the Game view) or press F9.
5. Type a level number > click "Load Level". Works from inside a level too.

TEST A CUSTOM LEVEL
1. Tools > Level Debug > Export Level To JSON... > type a level number > Export.
2. Tools > Level Debug > Open Custom Levels Folder > open custom_level.json in a text editor.
3. Change values (move_count, collect_data, star_point_1..3, board_data ...) > save.
   Tip: change "gid" to e.g. 9001 to keep the real level untouched.
4. Press Play > wait for the Lobby > LVL > "Load Custom JSON".
   The custom level only lives in memory until Play stops. Shipped files are never changed.

JSON format
{
  "game": { "gid": 9001, "game_mode": 0, "move_count": 30, "map_count": 1, "collect_data": "N1:20", ... },
  "map":  [ { "board_data": "...", "drop": "...", ... } ]
}
map_count, map[].gid and map[].mid are filled in automatically.

BUILD
- With tool:    Tools > Level Debug > Build With Debug Tool     -> Builds/<Platform>/WithDebug/
- Without tool: Tools > Level Debug > Build Without Debug Tool  -> Builds/<Platform>/Release/
- Uses the current platform (File > Build Profiles) and its enabled scene list. Project Settings are not changed.

Command line (Unity closed):
"C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\Unity Projects" -buildTarget Android -executeMethod DMScripts.Editor.LevelDebugBuildTools.BuildWithDebugTool -logFile build_debug.log
"C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:\Unity Projects" -buildTarget Android -executeMethod DMScripts.Editor.LevelDebugBuildTools.BuildWithoutDebugTool -logFile build_release.log
Optional: -buildOutput "D:\MyBuilds\game.apk"
