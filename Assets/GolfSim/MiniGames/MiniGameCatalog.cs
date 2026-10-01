using UnityEngine;

namespace GolfSimZA.MiniGames
{
    public enum MiniGameId
    {
        ClosestToPin, LongDrive, TargetRings, EdgeKnockout, CaptureFlags, JunkyardBlast, CreaturePond, DesertOasis
    }

    /// <summary>One mini game: what it is, how it is played and scored, and its options.</summary>
    public sealed class MiniGameInfo
    {
        public MiniGameId Id;
        public string Name;
        public string Group;
        public string Tagline;
        public string HowToPlay;
        public string Scoring;
        public Color Accent;
        /// <summary>Choices for shots per player (the first is the default).</summary>
        public int[] ShotChoices;
        public string ShotsLabel = "SHOTS PER PLAYER";
        /// <summary>CHALLENGE (scored) / EXPLORE (free play) modes.</summary>
        public bool HasExplore;
        /// <summary>Pin distance choice (closest to the pin).</summary>
        public bool HasDistance;
        public int MaxPlayers = 8;
    }

    /// <summary>The mini games on the MINI GAMES tab, in the order shown.</summary>
    public static class MiniGameCatalog
    {
        public const string Target = "TARGET CHALLENGES";
        public const string Classic = "CLASSIC COMPETITIONS";
        public const string Fun = "FUN & ADVENTURE";

        public static readonly MiniGameInfo[] All =
        {
            new MiniGameInfo
            {
                Id = MiniGameId.ClosestToPin, Name = "CLOSEST TO THE PIN", Group = Classic, Accent = new Color(0.96f, 0.72f, 0.23f),
                Tagline = "One flag, every shot measured to the hole",
                HowToPlay = "A pin is set on a green down the range. Every player hits their shots at it, taking turns. Where the ball stops is measured to the hole.",
                Scoring = "Each player's closest ball counts. The closest to the pin wins. A ball that finishes in the cup is an ACE.",
                ShotChoices = new[] { 3, 1, 2, 5 }, HasDistance = true
            },
            new MiniGameInfo
            {
                Id = MiniGameId.LongDrive, Name = "LONG DRIVE", Group = Classic, Accent = new Color(0.95f, 0.38f, 0.33f),
                Tagline = "Swing away - longest ball in the grid wins",
                HowToPlay = "A 40 m wide grid runs down the range. Take turns to hit your drives as far as you can - the ball must stop inside the grid to count.",
                Scoring = "Total distance of each player's longest ball in the grid. Balls outside the grid score nothing. Longest drive wins.",
                ShotChoices = new[] { 5, 3, 6 }, ShotsLabel = "BALLS PER PLAYER"
            },
            new MiniGameInfo
            {
                Id = MiniGameId.TargetRings, Name = "TARGET RINGS", Group = Target, Accent = new Color(0.35f, 0.70f, 1f),
                Tagline = "Land it in the rings for points",
                HowToPlay = "Three ringed targets sit at short, middle and long range. Pick your target and land the ball (where it first touches down) in the rings.",
                Scoring = "Inner ring 100, then 50, 25 and 10. Middle target x1.5, long target x2. Highest total wins.",
                ShotChoices = new[] { 10, 5, 15 }
            },
            new MiniGameInfo
            {
                Id = MiniGameId.EdgeKnockout, Name = "EDGE KNOCKOUT", Group = Target, Accent = new Color(0.90f, 0.30f, 0.55f),
                Tagline = "Stop it close to the edge - but never over",
                HowToPlay = "Play from the rim of a red-rock canyon. A grass cliff-top runs out to a sheer drop - the edge - with a scoring zone in front of it. Your ball must stop inside the zone: short of it, wide of it or over the edge (down into the canyon!) and you are out. The zone gets smaller every round.",
                Scoring = "With friends: every round the player furthest from the edge (or out of the zone) is knocked out - last one standing wins. On your own: survive as many rounds as you can.",
                ShotChoices = new[] { 12 }, ShotsLabel = "ROUNDS (MAX)"
            },
            new MiniGameInfo
            {
                Id = MiniGameId.CaptureFlags, Name = "CAPTURE THE FLAGS", Group = Target, Accent = new Color(0.30f, 0.85f, 0.50f),
                Tagline = "Claim flags - and steal them from your friends",
                HowToPlay = "Nine numbered flags stand in circles around the range. Land your ball inside a circle to take the flag - the circle fills with your colour and your ball mark stays. Someone else takes it from you only by landing inside AND closer to the flag than your ball.",
                Scoring = "The player holding the most flags after the last shot wins (tie: closest average shot).",
                ShotChoices = new[] { 6, 4, 8, 10 }
            },
            new MiniGameInfo
            {
                Id = MiniGameId.JunkyardBlast, Name = "JUNKYARD BLAST", Group = Fun, Accent = new Color(1f, 0.55f, 0.18f),
                Tagline = "Hit the barrels - same colours blow up together",
                HowToPlay = "A junkyard full of wrecked cars and coloured blast barrels. Land a ball next to a barrel to set it off - every barrel of the same colour nearby goes up with it in a chain reaction.",
                Scoring = "100 points per barrel, times the length of the chain (a 4-barrel chain = 1 600). Highest score wins.",
                ShotChoices = new[] { 8, 5, 10, 15 }
            },
            new MiniGameInfo
            {
                Id = MiniGameId.CreaturePond, Name = "CREATURE POND", Group = Fun, Accent = new Color(0.40f, 0.85f, 0.95f),
                Tagline = "Catch the creatures roaming the magic pond",
                HowToPlay = "Frogs, crabs, turtles, swans and a rare dragon roam around the pond. Land your ball on a creature to catch it. Explore mode has no scores and no limit - just catch them all.",
                Scoring = "Frog 50, crab 75, turtle 100, swan 150, dragon 400. Most points wins.",
                ShotChoices = new[] { 8, 5, 10, 15 }, HasExplore = true
            },
            new MiniGameInfo
            {
                Id = MiniGameId.DesertOasis, Name = "DESERT OASIS", Group = Fun, Accent = new Color(0.95f, 0.78f, 0.45f),
                Tagline = "Hunt the mystic creatures of the sands",
                HowToPlay = "Scorpions, lizards, fennec foxes, sand serpents and a golden phoenix hide around a desert oasis. Land your ball on a creature to catch it. Explore mode has no scores and no limit.",
                Scoring = "Scorpion 50, lizard 75, fox 100, serpent 150, golden phoenix 400. Most points wins.",
                ShotChoices = new[] { 8, 5, 10, 15 }, HasExplore = true
            }
        };

        public static MiniGameInfo Get(MiniGameId id)
        {
            foreach (MiniGameInfo g in All) if (g.Id == id) return g;
            return All[0];
        }
    }
}
