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
        public const int RequiredShots = 6;

        private BallFlightSimulator ballFlight;
        private bool subscribed;
        private string playerName;
        private int clubIndex;
        private string clubName;
        private readonly float[] carries = new float[RequiredShots];
        private readonly float[] totals = new float[RequiredShots];
        private int shotCount;
        private bool completed;

        public static bool IsActive => PlayerPrefs.GetInt(MapModeKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubMappingSession>() != null) return;
            GameObject go = new GameObject("GolfSimZA_ClubMappingSession");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubMappingSession>();
        }

        private void Awake() => Instance = this;

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
            // A club that was mapped (or cancelled) before can be mapped again: a new mapping
            // session always starts from shot 1 - only a scene reload during the same mapping
            // (e.g. the game menu) keeps the shots already hit.
            if (changed || completed)
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

            // All six shots in: ask SAVE CLUB DISTANCE or REDO (see OnGUI) once the ball has stopped.
            if (shotCount >= RequiredShots)
            {
                decisionShownAt = Time.unscaledTime + 0.8f;
                Debug.Log($"[GolfSimZA] Map My Bag: {RequiredShots} shots with {clubName} done - asking SAVE CLUB DISTANCE or REDO.");
            }
        }

        /// <summary>All six shots are in and the player has not chosen SAVE CLUB DISTANCE or REDO yet.</summary>
        public bool AwaitingDecision => IsActive && !completed && shotCount >= RequiredShots;

        private float decisionShownAt;
        private GUIStyle popupTitle, popupText, popupCell, popupValue;

        /// <summary>SAVE CLUB DISTANCE: saves the average of the six shots for this club and finishes mapping it.</summary>
        public void SaveClubDistance()
        {
            if (!AwaitingDecision) return;
            FinishMapping();
        }

        /// <summary>REDO: throws the six shots away and maps the club again from shot 1.</summary>
        public void RedoMapping()
        {
            if (!AwaitingDecision) return;
            Debug.Log($"[GolfSimZA] Map My Bag: {clubName} shots discarded - mapping again from shot 1.");
            Restart();
        }

        private float TotalAverage
        {
            get
            {
                if (shotCount == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < shotCount; i++) sum += totals[i];
                return sum / shotCount;
            }
        }

        /// <summary>The SAVE CLUB DISTANCE / REDO pop-up - only after all six shots are done.</summary>
        private void OnGUI()
        {
            if (!AwaitingDecision || Time.unscaledTime < decisionShownAt) return;
            GolfSimZA.UI.GolfSimTheme.Ensure();
            GUI.depth = -5000;
            if (popupTitle == null)
            {
                popupTitle = new GUIStyle(GolfSimZA.UI.GolfSimTheme.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 24, wordWrap = true };
                popupText = new GUIStyle(GolfSimZA.UI.GolfSimTheme.Body) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                popupCell = new GUIStyle(GolfSimZA.UI.GolfSimTheme.Label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
                popupValue = new GUIStyle(GolfSimZA.UI.GolfSimTheme.Heading) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            }

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = Mathf.Min(620f, Screen.width - 40f), h = 380f;
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, GolfSimZA.UI.GolfSimTheme.Overlay);
            float x = panel.x + 24f, cw = w - 48f, y = panel.y + 18f;
            GUI.Label(new Rect(x, y, cw, 34f), "MAP MY BAG  •  " + ClubName.ToUpperInvariant(), popupTitle);
            y += 38f;
            GUI.Label(new Rect(x, y, cw, 22f), PlayerName + "  •  all " + RequiredShots + " shots done", popupText);
            y += 34f;

            // The six carries.
            float cellW = (cw - 5f * 8f) / RequiredShots;
            for (int i = 0; i < RequiredShots; i++)
            {
                Rect c = new Rect(x + i * (cellW + 8f), y, cellW, 54f);
                GUI.Box(c, GUIContent.none, GolfSimZA.UI.GolfSimTheme.Card);
                GUI.Label(new Rect(c.x, c.y + 4f, c.width, 14f), "SHOT " + (i + 1), popupCell);
                GUI.Label(new Rect(c.x, c.y + 20f, c.width, 28f), Units.DistanceText(carries[i], "0.0"), popupValue);
            }
            y += 68f;
            GUI.Label(new Rect(x, y, cw, 30f), "CLUB DISTANCE  " + Units.DistanceText(AverageCarry, "0.0") + " CARRY   •   " + Units.DistanceText(TotalAverage, "0.0") + " TOTAL",
                new GUIStyle(popupValue) { fontSize = 20, normal = { textColor = GolfSimZA.UI.GolfSimTheme.Gold } });
            y += 44f;
            GUI.Label(new Rect(x, y, cw, 40f), "Save this distance for the " + ClubName + ", or REDO to throw these six shots away and hit them again.", popupText);
            y += 56f;

            float bw = (cw - 16f) * 0.5f;
            if (GUI.Button(new Rect(x, y, bw, 58f), "SAVE CLUB DISTANCE", new GUIStyle(GolfSimZA.UI.GolfSimTheme.AccentButton) { fontSize = 18 }))
                SaveClubDistance();
            else if (GUI.Button(new Rect(x + bw + 16f, y, bw, 58f), "REDO", new GUIStyle(GolfSimZA.UI.GolfSimTheme.Button) { fontSize = 18 }))
                RedoMapping();

            // The pop-up takes every click (nothing underneath can be pressed while it is open).
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel))
                e.Use();
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

        // ---------------------------------------------------------------- For the range HUD (TrackMan-style 6-shot block)

        /// <summary>The mapping session (the range HUD draws its 6-shot block above the club selector).</summary>
        public static ClubMappingSession Instance { get; private set; }
        public string PlayerName => playerName ?? "";
        public string ClubName => clubName ?? "";
        public int ShotCount => shotCount;
        public float Carry(int i) => i >= 0 && i < RequiredShots ? carries[i] : 0f;
        public float AverageCarry
        {
            get
            {
                if (shotCount == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < shotCount; i++) sum += carries[i];
                return sum / shotCount;
            }
        }

        /// <summary>START AGAIN: throw away the shots hit so far with this club and map it from shot 1.</summary>
        public void Restart()
        {
            shotCount = 0;
            for (int i = 0; i < RequiredShots; i++)
            {
                carries[i] = 0f;
                totals[i] = 0f;
            }
        }

        /// <summary>REDO LAST: remove the last shot (a mishit) and hit it again.</summary>
        public void UndoLast()
        {
            if (shotCount <= 0) return;
            shotCount--;
            carries[shotCount] = 0f;
            totals[shotCount] = 0f;
        }

        /// <summary>CANCEL: stop mapping and go back to the player's bag.</summary>
        public void Cancel()
        {
            PlayerPrefs.SetInt(MapModeKey, 0);
            PlayerPrefs.Save();
            completed = true;
            SceneManager.LoadScene("GolfSimZA_0_5_Players");
        }
    }
}
