using System;
using UnityEngine;

namespace GolfSimZA.Courses
{
    public enum GameFormatId { StrokePlay, MatchPlay, Stableford, Skins, Scramble, Shamble, Greensomes, BetterBall }

    /// <summary>One way of playing a round: how it works, how it is scored and what the game does.</summary>
    public sealed class GameFormatInfo
    {
        public GameFormatId Id;
        public string Name;
        /// <summary>One line for cards and the HUD.</summary>
        public string Short;
        public string HowItWorks;
        public string Scoring;
        public string WhyPlay;
        /// <summary>Players are split into teams (A, B, C, D).</summary>
        public bool Team;
        /// <summary>Every team member tees off, the team picks the best drive.</summary>
        public bool SelectDrive;
        /// <summary>Every team member hits every shot from the chosen spot (scramble).</summary>
        public bool SelectEveryShot;
        /// <summary>After the drive is picked the team plays one ball, taking turns.</summary>
        public bool AlternateAfterDrive;
        public int MinPlayers = 1;
        public int MinTeamSize = 1;
        public int MaxTeamSize = 8;
        /// <summary>Scored in points (higher is better) instead of strokes.</summary>
        public bool Points;
    }

    /// <summary>The game formats offered in Round Settings → MATCH SETTINGS → FORMAT.</summary>
    public static class GameFormats
    {
        public const int MaxTeams = 4;
        public static readonly string[] TeamLetters = { "A", "B", "C", "D" };
        public static readonly Color[] TeamColors =
        {
            new Color(0.95f, 0.32f, 0.30f), new Color(0.30f, 0.62f, 1f), new Color(1f, 0.80f, 0.22f), new Color(0.66f, 0.45f, 1f)
        };

        public static readonly GameFormatInfo[] All =
        {
            new GameFormatInfo
            {
                Id = GameFormatId.StrokePlay, Name = "Stroke Play", Short = "Every stroke counts - lowest total wins",
                HowItWorks = "Every player plays their own ball from the tee until it is in the hole, on every hole.",
                Scoring = "All strokes (and penalty strokes) are added up. The lowest total score for the round wins.",
                WhyPlay = "The classic way to play and the truest test of your game - every shot matters."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.MatchPlay, Name = "Match Play", Short = "Win holes, not strokes - most holes wins", MinPlayers = 2,
                HowItWorks = "Every player plays their own ball. Each hole is a separate contest.",
                Scoring = "The lowest score on a hole wins that hole; equal scores halve it. The match shows 2 UP, ALL SQUARE, DORMIE and is won when a player leads by more holes than are left (e.g. 3&2).",
                WhyPlay = "One bad hole only loses one hole, so you can attack. Great head-to-head drama."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.Stableford, Name = "Stableford", Short = "Points per hole - most points wins", Points = true,
                HowItWorks = "Every player plays their own ball. Each hole earns points compared with par.",
                Scoring = "Double bogey or worse 0 points, bogey 1, par 2, birdie 3, eagle 4, albatross 5. The most points over the round wins.",
                WhyPlay = "A blow-up hole costs at most 2 points, so the round stays fun - pick up when you can't score."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.Skins, Name = "Skins", Short = "Win a hole outright to win the skin", MinPlayers = 2,
                HowItWorks = "Every player plays their own ball. Each hole is worth one skin.",
                Scoring = "The single lowest score on a hole wins the skin. If two or more players tie for lowest, the skin carries over and the next hole is worth more. Most skins wins.",
                WhyPlay = "Every hole is a fresh bet and carry-overs build big pressure holes."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.Scramble, Name = "Scramble", Short = "Team picks the best shot every time",
                Team = true, SelectDrive = true, SelectEveryShot = true, MinTeamSize = 2, MaxTeamSize = 4, MinPlayers = 2,
                HowItWorks = "Everyone in the team tees off and the team picks the best drive. Everyone then plays their next shot from that spot, the team picks the best one again, and so on for every shot - approaches and putts too - until the ball is holed.",
                Scoring = "One team score per hole: the number of shots the team needed (plus penalties). The lowest team total wins.",
                WhyPlay = "The most relaxed format - a bad shot is wiped out as soon as a team-mate hits a good one."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.Shamble, Name = "Shamble", Short = "Best drive, then everyone plays their own ball",
                Team = true, SelectDrive = true, MinTeamSize = 2, MaxTeamSize = 4, MinPlayers = 2,
                HowItWorks = "Everyone in the team tees off and the team picks the best drive. Every player puts their ball on that spot and plays their own ball from there until it is in the hole.",
                Scoring = "The best (lowest) individual score in the team on each hole is the team score. The lowest team total wins.",
                WhyPlay = "Faster than normal golf and no pressure on the tee, but everyone still plays real golf on approaches and putts."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.Greensomes, Name = "Greensomes", Short = "Best drive, then alternate shot",
                Team = true, SelectDrive = true, AlternateAfterDrive = true, MinTeamSize = 2, MaxTeamSize = 2, MinPlayers = 2,
                HowItWorks = "Both players tee off on every hole and the team picks the best drive. From there the team plays one ball, taking turns: if Player A's drive is picked, Player B hits the second shot, Player A the third, and so on.",
                Scoring = "One team score per hole: the shots needed with the one ball (plus penalties). The lowest team total wins.",
                WhyPlay = "A safety net on the tee, then a real test of teamwork and chemistry on the fairway and green."
            },
            new GameFormatInfo
            {
                Id = GameFormatId.BetterBall, Name = "Better Ball (Four-Ball)", Short = "Own balls - the team counts the lower score",
                Team = true, MinTeamSize = 2, MaxTeamSize = 4, MinPlayers = 2,
                HowItWorks = "No picking a drive: every player plays their own ball completely on their own, from the tee into the hole.",
                Scoring = "On each hole the team counts whichever player made the lowest score. The lowest team total wins.",
                WhyPlay = "Everyone plays full golf but a partner can cover your bad holes."
            }
        };

        public static string[] Names
        {
            get
            {
                var names = new string[All.Length];
                for (int i = 0; i < All.Length; i++) names[i] = All[i].Name;
                return names;
            }
        }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            // Older saves: "Better Ball".
            if (!string.IsNullOrEmpty(name) && name.StartsWith("Better Ball", StringComparison.OrdinalIgnoreCase)) return (int)GameFormatId.BetterBall;
            return 0;
        }

        public static GameFormatInfo Get(string name) => All[IndexOf(name)];

        /// <summary>The format of the round being played (practice is always stroke play without scoring).</summary>
        public static GameFormatInfo Current => CourseSession.PracticeMode ? All[0] : Get(CourseSession.GameMode);

        /// <summary>Stableford points for a hole score.</summary>
        public static int StablefordPoints(int strokes, int par) => strokes <= 0 ? 0 : Mathf.Max(0, 2 + par - strokes);

        /// <summary>Default teams: players in order, two per team (A, A, B, B, ...).</summary>
        public static int[] DefaultTeams(int players, int teamSize = 2)
        {
            var teams = new int[players];
            for (int i = 0; i < players; i++) teams[i] = Mathf.Min(MaxTeams - 1, i / Mathf.Max(1, teamSize));
            return teams;
        }

        /// <summary>Why these players and teams cannot play this format, or null when they can.</summary>
        public static string Problem(GameFormatInfo format, int players, int[] teams)
        {
            if (players < format.MinPlayers) return format.Name + " needs at least " + format.MinPlayers + " players.";
            if (!format.Team) return null;
            if (teams == null || teams.Length != players) return "Put every player in a team.";
            var counts = new int[MaxTeams];
            foreach (int t in teams) counts[Mathf.Clamp(t, 0, MaxTeams - 1)]++;
            for (int t = 0; t < MaxTeams; t++)
            {
                if (counts[t] == 0) continue;
                if (counts[t] < format.MinTeamSize) return "TEAM " + TeamLetters[t] + " needs at least " + format.MinTeamSize + " players for " + format.Name + ".";
                if (counts[t] > format.MaxTeamSize) return "TEAM " + TeamLetters[t] + " can have at most " + format.MaxTeamSize + " players in " + format.Name + ".";
            }
            return null;
        }
    }
}
