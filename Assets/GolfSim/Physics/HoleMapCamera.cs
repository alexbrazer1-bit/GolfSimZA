using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Renders a top-down picture of the current hole (tee at the bottom, green at the top)
    /// into a texture for the hole map. Rendered once per hole, not every frame.
    /// </summary>
    public sealed class HoleMapCamera : MonoBehaviour
    {
        private Camera cam;
        private RenderTexture texture;

        public Texture Texture => texture;
        public bool HasImage { get; private set; }

        private void Awake()
        {
            var go = new GameObject("GolfSimZA_HoleMapCamera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.20f, 0.10f, 1f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 3000f;
            // Leave out the ball, markers and other helpers on the Ignore Raycast layer.
            cam.cullingMask = ~(1 << GroundProbe.IgnoreRaycastLayer);
            texture = new RenderTexture(512, 768, 24) { name = "GolfSimZA_HoleMap", antiAliasing = 2 };
            cam.targetTexture = texture;
        }

        private void OnDestroy()
        {
            if (texture != null) texture.Release();
        }

        /// <summary>Frames the hole points (tee, aim points, pin) and renders the picture.</summary>
        public void Render(List<Vector3> points)
        {
            if (cam == null || points == null || points.Count < 2) return;
            Vector3 tee = points[0], pin = points[points.Count - 1];
            Vector3 axis = pin - tee;
            axis.y = 0f;
            if (axis.sqrMagnitude < 1f) axis = Vector3.forward;
            axis.Normalize();
            Vector3 side = new Vector3(axis.z, 0f, -axis.x);

            float minAlong = 0f, maxAlong = 1f, maxSide = 15f, maxY = tee.y;
            foreach (Vector3 p in points)
            {
                Vector3 d = p - tee;
                float along = Vector3.Dot(d, axis);
                minAlong = Mathf.Min(minAlong, along);
                maxAlong = Mathf.Max(maxAlong, along);
                maxSide = Mathf.Max(maxSide, Mathf.Abs(Vector3.Dot(d, side)));
                maxY = Mathf.Max(maxY, p.y);
            }
            float length = maxAlong - minAlong + 50f;           // 25 m past the tee and the green
            float halfWidth = Mathf.Max(maxSide + 30f, length * 0.25f);
            float aspect = texture.width / (float)texture.height;
            float halfHeight = Mathf.Max(length * 0.5f, halfWidth / aspect);

            Vector3 centre = tee + axis * ((minAlong + maxAlong) * 0.5f);
            cam.orthographicSize = halfHeight;
            cam.transform.position = new Vector3(centre.x, maxY + 800f, centre.z);
            cam.transform.rotation = Quaternion.LookRotation(Vector3.down, axis);

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            HasImage = true;
        }

        /// <summary>Position inside the map rect (GUI coordinates) → point on the ground, or false.</summary>
        public bool FromMap(Vector2 guiPoint, Rect area, out Vector3 world)
        {
            world = Vector3.zero;
            if (cam == null || !HasImage || area.width < 1f || area.height < 1f) return false;
            Vector2 v = new Vector2((guiPoint.x - area.x) / area.width, 1f - (guiPoint.y - area.y) / area.height);
            if (v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
            Ray ray = cam.ViewportPointToRay(new Vector3(v.x, v.y, 0f));
            if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 5000f, ~(1 << GroundProbe.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore))
                world = hit.point;
            else
            {
                world = ray.origin;
                world.y = GroundProbe.HeightAt(world);
            }
            return true;
        }

        /// <summary>World point → position inside the map rect (GUI coordinates).</summary>
        public Vector2 ToMap(Vector3 world, Rect area)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            return new Vector2(area.x + v.x * area.width, area.y + (1f - v.y) * area.height);
        }
    }
}
