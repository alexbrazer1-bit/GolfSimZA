using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Finds the playing surface under a point. Works on the flat demo scenes and on
    /// imported course terrain. The golf ball itself lives on the Ignore Raycast layer
    /// so it is never detected as ground.
    /// </summary>
    public static class GroundProbe
    {
        public const int IgnoreRaycastLayer = 2;
        private const int Mask = ~(1 << IgnoreRaycastLayer);
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>Height used when nothing is found under the ball.</summary>
        public static float FallbackHeight { get; set; }

        public static bool TrySample(Vector3 point, out float height, out Vector3 normal)
        {
            // First look just above the ball (so an overhanging branch or bridge above
            // it is ignored), then from high up in case the ball stepped below the surface.
            if (Cast(new Vector3(point.x, point.y + 2f, point.z), 4000f, out height, out normal))
                return true;
            if (Cast(new Vector3(point.x, point.y + 1500f, point.z), 6000f, out height, out normal))
                return true;

            height = FallbackHeight;
            normal = Vector3.up;
            return false;
        }

        public static float HeightAt(Vector3 point)
        {
            TrySample(point, out float h, out _);
            return h;
        }

        private static bool Cast(Vector3 origin, float distance, out float height, out Vector3 normal)
        {
            int count = UnityEngine.Physics.RaycastNonAlloc(origin, Vector3.down, Hits, distance, Mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance < best)
                {
                    best = Hits[i].distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                height = FallbackHeight;
                normal = Vector3.up;
                return false;
            }

            height = Hits[bestIndex].point.y;
            normal = Hits[bestIndex].normal.y > 0.05f ? Hits[bestIndex].normal : Vector3.up;
            return true;
        }
    }
}
