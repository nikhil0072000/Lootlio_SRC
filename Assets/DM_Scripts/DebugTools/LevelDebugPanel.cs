#if UNITY_EDITOR || DEVELOPMENT_BUILD || LEVEL_DEBUG_TOOL
using UnityEngine;

namespace DMScripts.DebugTools
{
    /// <summary>
    /// On-screen level picker (IMGUI). Creates itself when Play starts; no scene setup needed.
    /// "LVL" button top-left or F9 toggles it.
    /// </summary>
    public class LevelDebugPanel : MonoBehaviour
    {
        private const string ToggleLabel = "LVL";
        private const KeyCode ToggleKey = KeyCode.F9;

        private bool isOpen;
        private bool isBusy;
        private string levelText = "1";
        private string customPath;
        private string status = "Enter a level number.";
        private Rect toggleRect;
        private Rect windowRect;
        private float uiScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (FindAnyObjectByType<LevelDebugPanel>() != null)
            {
                return;
            }
            GameObject go = new GameObject("[LevelDebugPanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelDebugPanel>();
        }

        private void Awake()
        {
            customPath = LevelDebugLoader.DefaultCustomFilePath;
            uiScale = Mathf.Max(1f, Screen.dpi / 160f);
            toggleRect = new Rect(8f, 8f, 70f * uiScale, 40f * uiScale);
            windowRect = new Rect(8f, 8f + toggleRect.height + 4f, 420f * uiScale, 330f * uiScale);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                isOpen = !isOpen;
            }
        }

        private void OnGUI()
        {
            if (GUI.Button(toggleRect, ToggleLabel))
            {
                isOpen = !isOpen;
            }
            if (isOpen)
            {
                windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Level Debug");
            }
        }

        private void DrawWindow(int id)
        {
            string reason;
            bool canStart = LevelDebugLoader.CanStartLevelNow(out reason) && !isBusy;
            if (isBusy)
            {
                GUILayout.Label("Working...");
            }
            else if (!canStart)
            {
                GUILayout.Label(reason);
            }

            GUILayout.Label("Levels loaded: " + MapData.LevelCount);
            GUILayout.Space(4f);

            GUILayout.Label("Level number");
            GUILayout.BeginHorizontal();
            levelText = GUILayout.TextField(levelText, 6, GUILayout.MinWidth(80f * uiScale));
            GUI.enabled = canStart;
            if (GUILayout.Button("< Prev"))
            {
                StepLevel(-1);
            }
            if (GUILayout.Button("Next >"))
            {
                StepLevel(1);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Load Level", GUILayout.Height(32f * uiScale)))
            {
                LoadTypedLevel();
            }
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label("Custom JSON path");
            customPath = GUILayout.TextField(customPath);
            GUI.enabled = canStart;
            if (GUILayout.Button("Load Custom JSON", GUILayout.Height(32f * uiScale)))
            {
                LoadCustomJson();
            }
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label(status);
            if (GUILayout.Button("Close"))
            {
                isOpen = false;
            }
            GUI.DragWindow();
        }

        private void StepLevel(int delta)
        {
            int level;
            if (!int.TryParse(levelText, out level))
            {
                level = 1;
            }
            level = Mathf.Max(1, level + delta);
            levelText = level.ToString();
            StartLevel(level);
        }

        private void LoadTypedLevel()
        {
            int level;
            if (!int.TryParse(levelText, out level) || level <= 0)
            {
                status = "Type a whole number of 1 or higher.";
                return;
            }
            StartLevel(level);
        }

        private void LoadCustomJson()
        {
            int gid;
            string error;
            if (!LevelDebugLoader.TryInjectCustomLevelFromFile(customPath, out gid, out error))
            {
                status = error;
                Debug.LogWarning("[LevelDebug] " + error);
                return;
            }
            levelText = gid.ToString();
            StartLevel(gid);
        }

        private void StartLevel(int gid)
        {
            if (!LevelDebugLoader.LevelExists(gid))
            {
                status = "Level " + gid + " does not exist.";
                return;
            }
            isOpen = false;
            StartCoroutine(RunStartLevel(gid));
        }

        private System.Collections.IEnumerator RunStartLevel(int gid)
        {
            isBusy = true;
            yield return LevelDebugLoader.StartLevel(gid, SetStatus);
            isBusy = false;
        }

        private void SetStatus(string message)
        {
            status = message;
        }
    }
}
#endif
