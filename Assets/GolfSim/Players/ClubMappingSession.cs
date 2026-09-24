using System;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Players
{
    public sealed class ClubMappingSession : MonoBehaviour
    {
        private const string MapModeKey = "GolfSimZA.MapMode";
        private const string PlayerKey = "GolfSimZA.MapPlayer";
        private const string ClubKey = "GolfSimZA.MapClubIndex";
        private const int RequiredShots = 6;

        private BallFlightSimulator ballFlight;
        private bool subscribed;
        private string playerName;
        private int clubIndex;
        private string clubName;
        private readonly float[] carries = new float[RequiredShots];
        private readonly float[] totals = new float[RequiredShots];
        private int shotCount;
        private bool completed;
        private GUIStyle title, subtitle, big, metric, button, muted, active;
        private Texture2D panelTexture, darkTexture, blueTexture, blueBrightTexture;
        private bool stylesReady;

        public static bool IsActive => PlayerPrefs.GetInt(MapModeKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubMappingSession>() != null) return;
            GameObject go = new GameObject("GolfSimZA_ClubMappingSession");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubMappingSession>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
        }

        private void Start()
        {
            ConfigureFromPrefs();
            TryAttach();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureFromPrefs();
            Unsubscribe();
            TryAttach();
        }

        private void ConfigureFromPrefs()
        {
            if (!IsActive)
            {
                completed = true;
                return;
            }

            string newPlayer = PlayerPrefs.GetString(PlayerKey, "Player 1");
            int newClub = Mathf.Clamp(PlayerPrefs.GetInt(ClubKey, 0), 0, GolfBagProfile.ClubCount - 1);
            bool changed = !string.Equals(playerName, newPlayer, StringComparison.Ordinal) || clubIndex != newClub;
            if (changed)
            {
                playerName = newPlayer;
                clubIndex = newClub;
                GolfBagProfile profile = GolfBagProfile.Load(playerName);
                clubName = profile.ClubNames[clubIndex];
                shotCount = 0;
                completed = false;
                for (int i = 0; i < RequiredShots; i++)
                {
                    carries[i] = 0f;
                    totals[i] = 0f;
                }
            }
        }

        private void TryAttach()
        {
            if (!IsActive || completed) return;
            ballFlight = FindFirstObjectByType<BallFlightSimulator>();
            if (ballFlight == null || subscribed) return;
            ballFlight.ShotCompleted += OnShotCompleted;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (ballFlight != null && subscribed)
                ballFlight.ShotCompleted -= OnShotCompleted;
            subscribed = false;
            ballFlight = null;
        }

        private void Update()
        {
            if (!IsActive || completed) return;
            if (!subscribed) TryAttach();
        }

        private void OnShotCompleted(ShotData shot, float carry, float total, float maxHeight, float flightTime)
        {
            if (!IsActive || completed || shotCount >= RequiredShots) return;
            if (!string.Equals(shot.ClubName, clubName, StringComparison.OrdinalIgnoreCase)) return;

            carries[shotCount] = Mathf.Max(0f, carry);
            totals[shotCount] = Mathf.Max(carries[shotCount], total);
            shotCount++;

            if (shotCount >= RequiredShots)
                FinishMapping();
        }

        private void FinishMapping()
        {
            GolfBagProfile profile = GolfBagProfile.Load(playerName);
            float carryAverage = 0f;
            float totalAverage = 0f;
            for (int i = 0; i < RequiredShots; i++)
            {
                carryAverage += carries[i];
                totalAverage += totals[i];
            }
            carryAverage /= RequiredShots;
            totalAverage /= RequiredShots;

            profile.CarryMeters[clubIndex] = carryAverage;
            profile.TotalMeters[clubIndex] = totalAverage;
            profile.InBag[clubIndex] = true;
            profile.Save(playerName);

            completed = true;
            PlayerPrefs.SetInt(MapModeKey, 0);
            PlayerPrefs.SetString("GolfSimZA.MapReturnPlayer", playerName);
            PlayerPrefs.Save();

            Debug.Log($"[GolfSimZA] Mapped {clubName} for {playerName}: {carryAverage:F1} m carry / {totalAverage:F1} m total from {RequiredShots} shots.");
            SceneManager.LoadScene("GolfSimZA_0_5_Players");
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            panelTexture = MakeTexture(new Color(0.02f, 0.055f, 0.065f, 0.97f));
            darkTexture = MakeTexture(new Color(0.005f, 0.025f, 0.03f, 0.98f));
            blueTexture = MakeTexture(new Color(0.03f, 0.48f, 0.82f, 1f));
            blueBrightTexture = MakeTexture(new Color(0.08f, 0.64f, 1f, 1f));
            title = MakeLabel(24, FontStyle.Bold, Color.white);
            subtitle = MakeLabel(13, FontStyle.Normal, new Color(0.74f, 0.82f, 0.85f));
            big = MakeLabel(30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            metric = MakeLabel(15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            muted = MakeLabel(12, FontStyle.Normal, new Color(0.70f, 0.78f, 0.81f), TextAnchor.MiddleCenter);
            active = MakeLabel(18, FontStyle.Bold, new Color(0.08f, 0.64f, 1f), TextAnchor.MiddleCenter);
            button = MakeButton(14, blueTexture);
            stylesReady = true;
        }

        private GUIStyle MakeLabel(int size, FontStyle style, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = size;
            s.fontStyle = style;
            s.alignment = alignment;
            s.normal.textColor = color;
            return s;
        }

        private GUIStyle MakeButton(int size, Texture2D background)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button);
            s.fontSize = size;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.background = background;
            s.hover.background = blueBrightTexture;
            s.active.background = blueTexture;
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            return s;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!IsActive || completed) return;
            EnsureStyles();
            GUI.depth = -1000;

            float panelWidth = Mathf.Min(720f, Screen.width - 70f);
            float panelHeight = Mathf.Min(520f, Screen.height - 70f);
            Rect area = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
            GUI.Box(area, GUIContent.none, new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(24, 24, 20, 20) });

            GUI.Label(new Rect(area.x + 24f, area.y + 18f, area.width - 48f, 34f), "MAP MY BAG", title);
            GUI.Label(new Rect(area.x + 24f, area.y + 54f, area.width - 48f, 24f), playerName + "  •  " + clubName, subtitle);
            GUI.Label(new Rect(area.x + 24f, area.y + 88f, area.width - 48f, 24f), "HIT 6 SHOTS WITH THIS CLUB", metric);

            float startY = area.y + 132f;
            float cardWidth = (area.width - 48f - 25f) / 2f;
            for (int i = 0; i < RequiredShots; i++)
            {
                float col = i % 2;
                float row = i / 2;
                Rect r = new Rect(area.x + 24f + col * (cardWidth + 25f), startY + row * 48f, cardWidth, 40f);
                GUI.Box(r, GUIContent.none, new GUIStyle(GUI.skin.box) { normal = { background = darkTexture } });
                string value = i < shotCount ? carries[i].ToString("F1") + " m carry" : "WAITING";
                GUI.Label(new Rect(r.x + 10f, r.y, 80f, r.height), "SHOT " + (i + 1), muted);
                GUI.Label(new Rect(r.x + 90f, r.y, r.width - 100f, r.height), value, i < shotCount ? active : muted);
            }

            float average = 0f;
            for (int i = 0; i < shotCount; i++) average += carries[i];
            if (shotCount > 0) average /= shotCount;
            GUI.Label(new Rect(area.x + 24f, area.y + 288f, area.width - 48f, 25f), "CURRENT AVERAGE", muted);
            GUI.Label(new Rect(area.x + 24f, area.y + 312f, area.width - 48f, 45f), shotCount > 0 ? average.ToString("F1") + " m" : "—", big);
            GUI.Label(new Rect(area.x + 24f, area.y + 360f, area.width - 48f, 22f), shotCount < RequiredShots ? "Press SPACE after each shot. The ball must come to rest before the next shot." : "Mapping complete — saving your 6-shot average…", subtitle);

            if (GUI.Button(new Rect(area.x + 24f, area.yMax - 62f, area.width - 48f, 42f), "CANCEL MAPPING", button))
            {
                PlayerPrefs.SetInt(MapModeKey, 0);
                PlayerPrefs.Save();
                completed = true;
                SceneManager.LoadScene("GolfSimZA_0_5_Players");
            }
        }
    }
}
