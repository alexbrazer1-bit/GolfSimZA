using System.Reflection;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class ClubMappingLauncherUI : MonoBehaviour
    {
        private PlayerSetupUI playerSetup;
        private FieldInfo editingBagField;
        private FieldInfo selectedPlayerField;
        private FieldInfo playersField;
        private FieldInfo bagProfileField;
        private bool showPicker;
        private int bagCount;
        private Vector2 scroll;
        private GUIStyle panelStyle, titleStyle, subtitleStyle, buttonStyle, activeButtonStyle, rowStyle, smallStyle;
        private Texture2D panelTexture, darkTexture, blueTexture, blueBrightTexture;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubMappingLauncherUI>() != null) return;
            GameObject go = new GameObject("GolfSimZA_ClubMappingLauncher");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubMappingLauncherUI>();
        }

        private void Start() => FindPlayerSetup();

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "GolfSimZA_0_5_Players")
            {
                showPicker = false;
                return;
            }
            if (playerSetup == null) FindPlayerSetup();
        }

        private void FindPlayerSetup()
        {
            playerSetup = FindFirstObjectByType<PlayerSetupUI>();
            if (playerSetup == null) return;
            System.Type type = typeof(PlayerSetupUI);
            editingBagField = type.GetField("editingBag", BindingFlags.Instance | BindingFlags.NonPublic);
            selectedPlayerField = type.GetField("selectedPlayerIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            playersField = type.GetField("players", BindingFlags.Instance | BindingFlags.NonPublic);
            bagProfileField = type.GetField("bagProfile", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private bool IsBagEditorOpen()
        {
            return playerSetup != null && editingBagField != null && (bool)editingBagField.GetValue(playerSetup);
        }

        private int SelectedPlayerIndex() => selectedPlayerField != null ? (int)selectedPlayerField.GetValue(playerSetup) : -1;

        private string SelectedPlayerName()
        {
            int index = SelectedPlayerIndex();
            var list = playersField != null ? playersField.GetValue(playerSetup) as System.Collections.Generic.List<string> : null;
            if (list == null || index < 0 || index >= list.Count) return "Player 1";
            return string.IsNullOrWhiteSpace(list[index]) ? "Player " + (index + 1) : list[index].Trim();
        }

        private GolfBagProfile Profile()
        {
            return bagProfileField != null ? bagProfileField.GetValue(playerSetup) as GolfBagProfile : null;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            panelTexture = MakeTexture(new Color(0.02f, 0.055f, 0.065f, 0.98f));
            darkTexture = MakeTexture(new Color(0.005f, 0.025f, 0.03f, 0.98f));
            blueTexture = MakeTexture(new Color(0.03f, 0.48f, 0.82f, 1f));
            blueBrightTexture = MakeTexture(new Color(0.08f, 0.64f, 1f, 1f));
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(18, 18, 14, 14) };
            titleStyle = Label(24, FontStyle.Bold, Color.white);
            subtitleStyle = Label(12, FontStyle.Normal, new Color(0.72f, 0.80f, 0.83f));
            rowStyle = Label(16, FontStyle.Bold, Color.white);
            smallStyle = Label(11, FontStyle.Normal, new Color(0.70f, 0.78f, 0.81f));
            buttonStyle = Button(13, blueTexture);
            activeButtonStyle = Button(13, blueBrightTexture);
            stylesReady = true;
        }

        private GUIStyle Label(int size, FontStyle style, Color color)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = size; s.fontStyle = style; s.normal.textColor = color;
            return s;
        }

        private GUIStyle Button(int size, Texture2D texture)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button);
            s.fontSize = size; s.fontStyle = FontStyle.Bold; s.fixedHeight = 36f;
            s.normal.background = texture; s.hover.background = blueBrightTexture; s.active.background = blueTexture;
            s.normal.textColor = Color.white; s.hover.textColor = Color.white; s.active.textColor = Color.white;
            return s;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D t = new Texture2D(1, 1); t.SetPixel(0, 0, color); t.Apply(); return t;
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != "GolfSimZA_0_5_Players" || !IsBagEditorOpen()) return;
            EnsureStyles();
            GUI.depth = -500;

            if (!showPicker)
            {
                float x = Screen.width - 225f;
                if (GUI.Button(new Rect(x, 24f, 195f, 40f), "CLUB LIBRARY • MAP", buttonStyle)) showPicker = true;
                return;
            }

            GolfBagProfile profile = Profile();
            if (profile == null) return;

            float width = Mathf.Min(760f, Screen.width - 60f);
            float height = Mathf.Min(650f, Screen.height - 60f);
            Rect area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(area, GUIContent.none, panelStyle);
            bagCount = profile.CountInBag();
            GUI.Label(new Rect(area.x + 20f, area.y + 15f, area.width - 160f, 30f), "CLUB LIBRARY", titleStyle);
            GUI.Label(new Rect(area.x + 20f, area.y + 47f, area.width - 40f, 20f), SelectedPlayerName() + "  •  " + bagCount + " / " + GolfBagProfile.MaxBagClubs + " clubs selected", subtitleStyle);
            if (GUI.Button(new Rect(area.xMax - 120f, area.y + 15f, 90f, 34f), "CLOSE", buttonStyle)) { showPicker = false; return; }

            scroll = GUI.BeginScrollView(new Rect(area.x + 16f, area.y + 82f, area.width - 32f, area.height - 102f), scroll, new Rect(0, 0, area.width - 55f, GolfBagProfile.ClubCount * 46f));
            for (int i = 0; i < GolfBagProfile.ClubCount; i++)
            {
                float y = i * 46f;
                GUI.Box(new Rect(0, y, area.width - 55f, 40f), GUIContent.none, new GUIStyle(GUI.skin.box) { normal = { background = darkTexture } });
                GUI.Label(new Rect(12f, y + 2f, 180f, 36f), profile.ClubNames[i], rowStyle);
                GUI.Label(new Rect(200f, y + 4f, 70f, 32f), profile.Lofts[i].ToString("F1") + "°", smallStyle);
                string mapped = profile.CarryMeters[i] > 0f ? profile.CarryMeters[i].ToString("F0") + " m carry" : "NOT MAPPED";
                GUI.Label(new Rect(285f, y + 4f, 145f, 32f), mapped, smallStyle);

                bool inBag = profile.InBag[i];
                bool canAdd = !inBag && bagCount < GolfBagProfile.MaxBagClubs;
                if (GUI.Button(new Rect(area.width - 350f, y + 2f, 120f, 36f), inBag ? "✓ IN BAG" : (canAdd ? "ADD TO BAG" : "BAG FULL"), inBag ? activeButtonStyle : buttonStyle))
                {
                    if (inBag)
                    {
                        profile.InBag[i] = false;
                        bagCount--;
                        profile.Save(SelectedPlayerName());
                    }
                    else if (canAdd)
                    {
                        profile.InBag[i] = true;
                        bagCount++;
                        profile.Save(SelectedPlayerName());
                    }
                }

                GUI.enabled = inBag;
                if (GUI.Button(new Rect(area.width - 210f, y + 2f, 140f, 36f), "MAP 6 SHOTS", activeButtonStyle))
                {
                    PlayerPrefs.SetInt("GolfSimZA.MapMode", 1);
                    PlayerPrefs.SetString("GolfSimZA.MapPlayer", SelectedPlayerName());
                    PlayerPrefs.SetInt("GolfSimZA.MapClubIndex", i);
                    PlayerPrefs.Save();
                    showPicker = false;
                    SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }
    }
}
