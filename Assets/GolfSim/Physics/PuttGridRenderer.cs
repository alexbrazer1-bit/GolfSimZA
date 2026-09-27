using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Putt grid: small arrows on the green pointing downhill, coloured by slope
    /// (green = flat, yellow = moderate, red = steep). Shown from the game menu.
    /// </summary>
    public sealed class PuttGridRenderer : MonoBehaviour
    {
        private GameObject gridObject;
        private Mesh mesh;
        private Material material;

        public void Show(Vector3 pin, Vector3 ball)
        {
            Vector3 d = ball - pin;
            d.y = 0f;
            float radius = Mathf.Clamp(d.magnitude + 3f, 6f, 18f);
            const float spacing = 0.9f;

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (float x = -radius; x <= radius; x += spacing)
            {
                for (float z = -radius; z <= radius; z += spacing)
                {
                    if (x * x + z * z > radius * radius) continue;
                    Vector3 p = new Vector3(pin.x + x, pin.y + 5f, pin.z + z);
                    if (!GroundProbe.TrySample(p, out float h, out Vector3 n)) continue;

                    Vector3 downhill = new Vector3(n.x, 0f, n.z);
                    float slopePercent = Mathf.Tan(Mathf.Acos(Mathf.Clamp01(n.y))) * 100f;
                    if (downhill.sqrMagnitude < 0.000001f) downhill = Vector3.forward;
                    downhill.Normalize();
                    Vector3 side = new Vector3(downhill.z, 0f, -downhill.x);

                    float length = Mathf.Lerp(0.18f, 0.55f, Mathf.Clamp01(slopePercent / 4f));
                    Vector3 c = new Vector3(p.x, h + 0.025f, p.z);
                    Color color = slopePercent < 1f ? new Color(0.25f, 0.9f, 0.45f, 0.9f)
                        : slopePercent < 2.5f ? new Color(1f, 0.85f, 0.2f, 0.9f)
                        : new Color(1f, 0.3f, 0.2f, 0.9f);

                    int start = vertices.Count;
                    vertices.Add(c + downhill * length * 0.6f);
                    vertices.Add(c - downhill * length * 0.4f + side * 0.09f);
                    vertices.Add(c - downhill * length * 0.4f - side * 0.09f);
                    colors.Add(color); colors.Add(color); colors.Add(color);
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                }
            }

            if (gridObject == null)
            {
                gridObject = new GameObject("PuttGrid");
                gridObject.layer = GroundProbe.IgnoreRaycastLayer;
                mesh = new Mesh { name = "PuttGridMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                gridObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = gridObject.AddComponent<MeshRenderer>();
                material = new Material(Shader.Find("Sprites/Default"));
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            gridObject.SetActive(true);
        }

        public void Hide()
        {
            if (gridObject != null) gridObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (gridObject != null) Destroy(gridObject);
        }
    }
}
