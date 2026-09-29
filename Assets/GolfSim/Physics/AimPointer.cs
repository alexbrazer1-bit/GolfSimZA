using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// The aim target on the ground (a gold ring). The player drags it with the mouse - left-click
    /// on the ring and drag - or right-clicks anywhere on the ground to put it there. Moving it
    /// turns the shot (direction) and sets the distance the player wants to hit. The owner reads
    /// <see cref="Point"/> and listens to <see cref="Moved"/>.
    /// </summary>
    public sealed class AimPointer : MonoBehaviour
    {
        private const int Segments = 40;
        private LineRenderer ring, cross;
        private Vector3 origin;

        public Vector3 Point { get; private set; }
        public bool Dragging { get; private set; }
        public bool Visible => ring != null && ring.gameObject.activeSelf;

        /// <summary>Raised while the player moves the target.</summary>
        public event Action<Vector3> Moved;

        /// <summary>When false (ball in flight, menu open ...) the mouse does nothing.</summary>
        public Func<bool> CanInteract;

        /// <summary>Longest aim distance from the ball (metres).</summary>
        public float MaxDistance = 400f;

        private void Awake()
        {
            ring = MakeLine("GolfSimZA_AimTarget", Segments + 1, new Color(1f, 0.82f, 0.25f, 0.95f));
            cross = MakeLine("GolfSimZA_AimTargetCross", 5, new Color(1f, 1f, 1f, 0.9f));
            Hide();
        }

        private LineRenderer MakeLine(string name, int points, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = GroundProbe.IgnoreRaycastLayer;
            LineRenderer line = go.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            line.material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            line.positionCount = points;
            line.useWorldSpace = true;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            return line;
        }

        /// <summary>Puts the target at a point (kept on the ground, within reach of the ball).</summary>
        public void Set(Vector3 ballPosition, Vector3 point)
        {
            origin = ballPosition;
            Vector3 flat = point - ballPosition;
            flat.y = 0f;
            if (flat.magnitude > MaxDistance) point = ballPosition + flat.normalized * MaxDistance;
            point.y = GroundProbe.HeightAt(point);
            Point = point;
            ring.gameObject.SetActive(true);
            cross.gameObject.SetActive(true);
            Redraw();
        }

        public void Hide()
        {
            Dragging = false;
            if (ring != null) ring.gameObject.SetActive(false);
            if (cross != null) cross.gameObject.SetActive(false);
        }

        /// <summary>Distance from the ball to the target along the ground.</summary>
        public float Distance
        {
            get
            {
                Vector3 d = Point - origin;
                d.y = 0f;
                return d.magnitude;
            }
        }

        private void Redraw()
        {
            Camera cam = Camera.main;
            float camDistance = cam != null ? Vector3.Distance(cam.transform.position, Point) : 50f;
            float radius = Mathf.Clamp(camDistance * 0.012f, 0.5f, 6f);
            float width = Mathf.Clamp(camDistance * 0.0025f, 0.04f, 0.8f);
            ring.widthMultiplier = width;
            cross.widthMultiplier = width;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                Vector3 p = Point + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                p.y = GroundProbe.HeightAt(p) + 0.06f;
                ring.SetPosition(i, p);
            }
            float c = radius * 0.45f;
            Vector3[] pts =
            {
                Point + new Vector3(-c, 0f, 0f), Point + new Vector3(c, 0f, 0f), Point,
                Point + new Vector3(0f, 0f, -c), Point + new Vector3(0f, 0f, c)
            };
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i].y = GroundProbe.HeightAt(pts[i]) + 0.07f;
                cross.SetPosition(i, pts[i]);
            }
        }

        private void Update()
        {
            if (!Visible) return;
            Redraw(); // size follows the camera distance
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;
            if (CanInteract != null && !CanInteract())
            {
                Dragging = false;
                return;
            }

            Vector2 mp = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Vector3 sp = cam.WorldToScreenPoint(Point);
                if (sp.z > 0f && Vector2.Distance(mp, new Vector2(sp.x, sp.y)) < 48f) Dragging = true;
            }
            if (mouse.leftButton.wasReleasedThisFrame) Dragging = false;

            bool place = mouse.rightButton.wasPressedThisFrame || (Dragging && mouse.leftButton.isPressed);
            if (place && GroundUnder(cam, mp, out Vector3 ground))
            {
                Set(origin, ground);
                Moved?.Invoke(Point);
            }
        }

        private static bool GroundUnder(Camera cam, Vector2 screen, out Vector3 point)
        {
            point = Vector3.zero;
            Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 6000f, ~(1 << GroundProbe.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            // Flat range without far colliders: intersect with the ground plane under the ball.
            var plane = new Plane(Vector3.up, new Vector3(0f, GroundProbe.FallbackHeight, 0f));
            if (plane.Raycast(ray, out float enter) && enter < 6000f)
            {
                point = ray.GetPoint(enter);
                return true;
            }
            return false;
        }
    }
}
