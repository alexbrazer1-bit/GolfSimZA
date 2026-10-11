using System;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Surface type under a point for courses that GolfSimZA builds itself (OpenStreetMap
    /// courses): one painted ground with a surface map instead of separately named objects.
    /// Used by the green check, the rough grass and the hole map. Ids match
    /// GolfSimZA.Courses.Osm.OsmSurface.
    /// </summary>
    public static class CourseSurfaceMap
    {
        public const int Unknown = -1, Rough = 0, Fairway = 1, Green = 2, Tee = 3, Bunker = 4, Path = 5, Fringe = 6, Native = 7;

        private static Func<Vector3, int> lookup;
        private static Collider ground;

        /// <summary>True while a built course is loaded.</summary>
        public static bool Active => lookup != null;

        /// <summary>The built course's ground collider (its terrain).</summary>
        public static Collider Ground => ground;

        public static void Set(Func<Vector3, int> surfaceAt, Collider groundCollider)
        {
            lookup = surfaceAt;
            ground = groundCollider;
        }

        public static void Clear()
        {
            lookup = null;
            ground = null;
        }

        /// <summary>Surface id at a world point, or Unknown outside the built course.</summary>
        public static int At(Vector3 world) => lookup != null ? lookup(world) : Unknown;

        public static bool IsGroundCollider(Collider c) => c != null && ground != null && c == ground;
    }
}
