using System;
using System.Collections.Generic;
using System.Reflection;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class ClubDistanceAdvisorUI : MonoBehaviour
    {
        private RoundGameplayUI roundUI;
        private FieldInfo playerNamesField;
        private FieldInfo activePlayerField;
        private Transform ball;
        private Transform pin;
        private Vector2 scroll;
        private string selectedPlayer = "Player 1";
        private float targetDistance;
        private GUIStyle panel, title, subtitle, row, value, recommend, button;
        private Texture2D panelTexture, darkTexture, blueTexture, brightTexture;
        private bool ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubDistanceAdvisorUI>() != null) return;
            GameObject go = new GameObject("GolfSimZA_ClubDistanceAdvisor");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubDistanceAdvisorUI>();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "GolfSimZA_0_6_PlayRound") return;
            if (roundUI == null) FindRoundUI();
            FindTargets();
            RefreshTarget();
        }

        private void FindRoundUI()
        {
            roundUI = FindFirstObjectByType<RoundGameplayUI>();
            if (roundUI == null) return;
            Type t = typeof(RoundGameplayUI);
            playerNamesField = t.GetField("playerNames", BindingFlags.Instance | BindingFlags.NonPublic);
            activePlayerField = t.GetField("activePlayerIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void FindTargets()
        {
            if (ball == null)
            {
                GameObject b = GameObject.Find("Ball");
                if (b != null) ball = b.transform;
            }
            if (pin == null)
            {
                GameObject p = GameObject.Find("Pin");
                if (p == null) p = GameObject.Find("HolePin");
                if (p != null) pin = p.transform;
            }
        }

        private void RefreshTarget()
        {
            if (roundUI != null && playerNamesField != null && activePlayerField != null)
            {
                string[] names = playerNamesField.GetValue(roundUI) as string[];
                int index = (int)activePlayerField.GetValue(roundUI);
                if (names != null && index >= 0 && index < names.Length && !string.IsNullOrWhiteSpace(names[index]))
                    selectedPlayer = names[index];
            }

            if (ball != null && pin != null)
                targetDistance = Vector3.Distance(ball.position, pin.position);
        }

        private GolfBagProfile Profile() => GolfBagProfile.Load(selectedPlayer);

        private List<int> BestClubs(GolfBagProfile profile)
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < GolfBagProfile.ClubCount; i++)
                if (profile.InBag[i] && profile.CarryMeters[i] > 0.1f)
                    indices.Add(i);

            indices.Sort((a, b) => Mathf.Abs(profile.CarryMeters[a] - targetDistance).CompareTo(Mathf.Abs(profile.CarryMeters[b] - targetDistance)));
            if (indices.Count > 3) indices.RemoveRange(3, indices.Count - 3);
            return indices;
        }

        private void EnsureStyles()
        {
            if (ready) return;
            panelTexture = MakeTexture(new Color(0.02f, 0.055f, 0.065f, 0.97f));
            darkTexture = MakeTexture(new Color(0.005f, 0.025f, 0.03f, 0.98f));
            blueTexture = MakeTexture(new Color(0.03f, 0.48f, 0.82f, 1f));
            brightTexture = MakeTexture(new Color(0.08f, 0.64f, 1f, 1f));
            panel = Box(panelTexture);
            title = Label(14, FontStyle.Bold, Color.white);
            subtitle = Label(10, FontStyle.Normal, new Color(0.72f, 0.80f, 0.83f));
            row = Label(12, FontStyle.Bold, Color.white);
            value = Label(12, FontStyle.Bold, new Color(0.08f, 0.64f, 1f));
            recommend = Label(11, FontStyle.Bold, Color.white);
            button = Button(11, blueTexture);
            ready = true;
        }

        private GUIStyle Box(Texture2D texture)
        {
            GUIStyle s = new GUIStyle(GUI.skin.box);
            s.normal.background = texture;
            s.padding = new RectOffset(10, 10, 8, 8);
            return s;
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
            s.fontSize = size; s.fontStyle = FontStyle.Bold;
            s.normal.background = texture; s.hover.background = brightTexture;
            s.normal.textColor = Color.white; s.hover.textColor = Color.white;
            return s;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D t = new Texture2D(1, 1); t.SetPixel(0, 0, color); t.Apply(); return t;
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != "GolfSimZA_0_6_PlayRound") return;
            EnsureStyles();
            GolfBagProfile profile = Profile();
            List<int> best = BestClubs(profile);
            if (best.Count == 0) return;

            float width = Mathf.Min(265f, Screen.width * 0.22f);
            float x = Screen.width - width - 16f;
            float y = Screen.height - 250f;
            float height = 226f;
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, panel);
            GUI.Label(new Rect(x + 12f, y + 9f, width - 24f, 20f), "CLUB RECOMMENDATION", title);
            GUI.Label(new Rect(x + 12f, y + 31f, width - 24f, 17f), selectedPlayer + "  •  mapped bag", subtitle);
            GUI.Label(new Rect(x + 12f, y + 52f, width - 24f, 24f), targetDistance > 0f ? targetDistance.ToString("F0") + " m TO PIN" : "DISTANCE TO PIN", value);

            int recommended = best[0];
            GUI.Label(new Rect(x + 12f, y + 79f, width - 24f, 17f), "BEST MATCH", subtitle);
            GUI.Label(new Rect(x + 12f, y + 97f, width - 135f, 26f), profile.ClubNames[recommended], recommend);
            GUI.Label(new Rect(x + width - 125f, y + 97f, 110f, 26f), profile.CarryMeters[recommended].ToString("F0") + " m", value);

            float rowY = y + 128f;
            for (int n = 0; n < best.Count; n++)
            {
                int i = best[n];
                float gap = Mathf.Abs(profile.CarryMeters[i] - targetDistance);
                GUI.Label(new Rect(x + 12f, rowY, 100f, 20f), (n + 1) + ". " + profile.ClubNames[i], row);
                GUI.Label(new Rect(x + 115f, rowY, 65f, 20f), profile.CarryMeters[i].ToString("F0") + " m", value);
                GUI.Label(new Rect(x + 180f, rowY, 55f, 20f), (gap <= 5f ? "IDEAL" : gap.ToString("F0") + " m"), subtitle);
                if (GUI.Button(new Rect(x + width - 83f, rowY - 1f, 68f, 24f), "SELECT", button))
                    SelectClub(profile.ClubNames[i]);
                rowY += 29f;
            }
        }

        private void SelectClub(string clubName)
        {
            PlayerPrefs.SetString("GolfSimZA.RoundClubName", clubName);
            PlayerPrefs.Save();
            Debug.Log("[GolfSimZA] Recommended club selected: " + clubName);
        }
    }
}
