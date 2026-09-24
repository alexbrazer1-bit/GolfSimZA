using System;
using System.Reflection;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Stage 0.9.5: presents the six-shot mapping result as a clean practice summary.
    /// The result is tied to the current player/club/shot set so every newly mapped club can be reviewed once.
    /// </summary>
    public sealed class ClubMappingResultUI : MonoBehaviour
    {
        private const string SceneName = "GolfSimZA_0_5_Players";
        private const string DismissedSignatureKey = "GolfSimZA.MappingResultDismissedSignature";

        private FieldInfo playerField;
        private FieldInfo clubField;
        private FieldInfo carriesField;
        private FieldInfo totalsField;
        private FieldInfo shotCountField;
        private FieldInfo completedField;
        private bool visible;
        private string playerName = "Player 1";
        private string clubName = "Driver";
        private string resultSignature = string.Empty;
        private readonly float[] carries = new float[6];
        private readonly float[] totals = new float[6];
        private int shotCount;
        private Vector2 scroll;
        private GUIStyle panel, title, subtitle, metric, value, muted, button, accent;
        private Texture2D panelTexture, darkTexture, blueTexture, blueBrightTexture;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubMappingResultUI>() != null) return;
            var go = new GameObject("GolfSimZA_ClubMappingResultUI");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubMappingResultUI>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void Start() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            visible = false;
            if (scene.name != SceneName) return;
            TryReadCompletedMapping();
        }

        private void TryReadCompletedMapping()
        {
            var session = FindFirstObjectByType<ClubMappingSession>();
            if (session == null) return;

            Type type = session.GetType();
            playerField = type.GetField("playerName", BindingFlags.Instance | BindingFlags.NonPublic);
            clubField = type.GetField("clubName", BindingFlags.Instance | BindingFlags.NonPublic);
            carriesField = type.GetField("carries", BindingFlags.Instance | BindingFlags.NonPublic);
            totalsField = type.GetField("totals", BindingFlags.Instance | BindingFlags.NonPublic);
            shotCountField = type.GetField("shotCount", BindingFlags.Instance | BindingFlags.NonPublic);
            completedField = type.GetField("completed", BindingFlags.Instance | BindingFlags.NonPublic);

            if (completedField == null || !(bool)completedField.GetValue(session)) return;
            if (shotCountField == null || (int)shotCountField.GetValue(session) < 6) return;

            playerName = playerField != null ? (string)playerField.GetValue(session) : "Player 1";
            clubName = clubField != null ? (string)clubField.GetValue(session) : "Club";
            var carryValues = carriesField != null ? carriesField.GetValue(session) as float[] : null;
            var totalValues = totalsField != null ? totalsField.GetValue(session) as float[] : null;
            shotCount = 6;
            for (int i = 0; i < 6; i++)
            {
                carries[i] = carryValues != null && i < carryValues.Length ? carryValues[i] : 0f;
                totals[i] = totalValues != null && i < totalValues.Length ? totalValues[i] : 0f;
            }

            resultSignature = BuildSignature();
            if (PlayerPrefs.GetString(DismissedSignatureKey, string.Empty) == resultSignature) return;
            visible = true;
        }

        private string BuildSignature()
        {
            string signature = playerName + "|" + clubName;
            for (int i = 0; i < 6; i++) signature += "|" + carries[i].ToString("F2") + ":" + totals[i].ToString("F2");
            return signature;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            panelTexture = MakeTexture(new Color(0.02f, 0.055f, 0.065f, 0.985f));
            darkTexture = MakeTexture(new Color(0.005f, 0.025f, 0.03f, 0.98f));
            blueTexture = MakeTexture(new Color(0.03f, 0.48f, 0.82f, 1f));
            blueBrightTexture = MakeTexture(new Color(0.08f, 0.64f, 1f, 1f));
            panel = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(22, 22, 18, 18) };
            title = Label(25, FontStyle.Bold, Color.white);
            subtitle = Label(13, FontStyle.Normal, new Color(0.74f, 0.82f, 0.85f));
            metric = Label(12, FontStyle.Bold, new Color(0.70f, 0.78f, 0.81f), TextAnchor.MiddleCenter);
            value = Label(22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            muted = Label(11, FontStyle.Normal, new Color(0.65f, 0.74f, 0.77f), TextAnchor.MiddleCenter);
            accent = Label(16, FontStyle.Bold, new Color(0.08f, 0.64f, 1f), TextAnchor.MiddleCenter);
            button = Button(13, blueTexture);
            stylesReady = true;
        }

        private GUIStyle Label(int size, FontStyle fontStyle, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.alignment = alignment;
            style.normal.textColor = color;
            return style;
        }

        private GUIStyle Button(int size, Texture2D background)
        {
            var style = new GUIStyle(GUI.skin.button);
            style.fontSize = size;
            style.fontStyle = FontStyle.Bold;
            style.fixedHeight = 40f;
            style.normal.background = background;
            style.hover.background = blueBrightTexture;
            style.active.background = blueTexture;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private float Average(float[] values)
        {
            float sum = 0f;
            for (int i = 0; i < shotCount; i++) sum += values[i];
            return shotCount == 0 ? 0f : sum / shotCount;
        }

        private float StandardDeviation(float[] values)
        {
            float avg = Average(values);
            float sum = 0f;
            for (int i = 0; i < shotCount; i++)
            {
                float delta = values[i] - avg;
                sum += delta * delta;
            }
            return shotCount == 0 ? 0f : Mathf.Sqrt(sum / shotCount);
        }

        private void OnGUI()
        {
            if (!visible || SceneManager.GetActiveScene().name != SceneName) return;
            EnsureStyles();
            GUI.depth = -1200;

            float width = Mathf.Min(780f, Screen.width - 70f);
            float height = Mathf.Min(590f, Screen.height - 70f);
            Rect area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(area, GUIContent.none, panel);

            GUI.Label(new Rect(area.x + 22f, area.y + 18f, area.width - 44f, 32f), "MAPPING COMPLETE", title);
            GUI.Label(new Rect(area.x + 22f, area.y + 52f, area.width - 44f, 24f), playerName + "  •  " + clubName + "  •  6-shot distance profile", subtitle);

            float carryAverage = Average(carries);
            float totalAverage = Average(totals);
            float consistency = StandardDeviation(carries);
            float best = carries[0];
            float worst = carries[0];
            for (int i = 1; i < shotCount; i++)
            {
                best = Mathf.Max(best, carries[i]);
                worst = Mathf.Min(worst, carries[i]);
            }

            float cardY = area.y + 92f;
            float cardWidth = (area.width - 66f) / 4f;
            DrawMetric(new Rect(area.x + 22f, cardY, cardWidth, 74f), "CARRY AVERAGE", carryAverage.ToString("F1") + " m");
            DrawMetric(new Rect(area.x + 30f + cardWidth, cardY, cardWidth, 74f), "TOTAL AVERAGE", totalAverage.ToString("F1") + " m");
            DrawMetric(new Rect(area.x + 38f + cardWidth * 2f, cardY, cardWidth, 74f), "BEST CARRY", best.ToString("F1") + " m");
            DrawMetric(new Rect(area.x + 46f + cardWidth * 3f, cardY, cardWidth, 74f), "SPREAD", consistency.ToString("F1") + " m SD");

            GUI.Label(new Rect(area.x + 22f, area.y + 182f, area.width - 44f, 24f), "SIX SHOT RESULTS", accent);
            float tableTop = area.y + 214f;
            float tableHeight = area.height - 292f;
            Rect table = new Rect(area.x + 22f, tableTop, area.width - 44f, tableHeight);
            GUI.Box(table, GUIContent.none, new GUIStyle(GUI.skin.box) { normal = { background = darkTexture } });

            scroll = GUI.BeginScrollView(new Rect(table.x + 8f, table.y + 8f, table.width - 16f, table.height - 16f), scroll, new Rect(0, 0, table.width - 34f, 6 * 44f + 10f));
            for (int i = 0; i < 6; i++)
            {
                float y = i * 44f;
                GUI.Label(new Rect(10f, y, 80f, 38f), "SHOT " + (i + 1), metric);
                GUI.Label(new Rect(100f, y, 180f, 38f), carries[i].ToString("F1") + " m carry", value);
                GUI.Label(new Rect(300f, y, 180f, 38f), totals[i].ToString("F1") + " m total", value);
            }
            GUI.EndScrollView();

            GUI.Label(new Rect(area.x + 22f, area.yMax - 112f, area.width - 44f, 24f), "Your bag now uses the average of all 6 shots for this club.", subtitle);
            GUI.Label(new Rect(area.x + 22f, area.yMax - 88f, area.width - 44f, 20f), "Carry range: " + worst.ToString("F1") + "–" + best.ToString("F1") + " m  •  Consistency: ±" + consistency.ToString("F1") + " m", muted);

            if (GUI.Button(new Rect(area.x + 22f, area.yMax - 52f, area.width - 44f, 40f), "DONE  •  RETURN TO PLAYER / BAG", button))
            {
                PlayerPrefs.SetString(DismissedSignatureKey, resultSignature);
                PlayerPrefs.Save();
                visible = false;
            }
        }

        private void DrawMetric(Rect rect, string label, string number)
        {
            GUI.Box(rect, GUIContent.none, new GUIStyle(GUI.skin.box) { normal = { background = darkTexture } });
            GUI.Label(new Rect(rect.x, rect.y + 6f, rect.width, 20f), label, metric);
            GUI.Label(new Rect(rect.x, rect.y + 28f, rect.width, 38f), number, value);
        }
    }
}
