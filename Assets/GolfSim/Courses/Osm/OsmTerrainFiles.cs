using System;
using System.IO;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>Surface types painted on an OpenStreetMap course (one byte per ground cell).</summary>
    public static class OsmSurface
    {
        public const byte Rough = 0, Fairway = 1, Green = 2, Tee = 3, Bunker = 4, Path = 5, Fringe = 6, Native = 7;
        public const int Count = 8;
        public static readonly string[] Names = { "Rough", "Fairway", "Green", "Tee", "Bunker", "Path", "Fringe", "Native" };
    }

    /// <summary>
    /// Ground height grid of an OpenStreetMap course (heights.bin). Points are spaced
    /// Size / (Resolution - 1) metres apart, starting at (OriginX, OriginZ) in course metres.
    /// </summary>
    public sealed class OsmHeightField
    {
        public int Resolution;
        public float Size;
        public float OriginX, OriginZ;
        public float[] Heights; // [z * Resolution + x], metres above sea level

        public float Spacing => Size / (Resolution - 1);

        public float MinHeight
        {
            get { float m = float.MaxValue; foreach (float h in Heights) if (h < m) m = h; return m; }
        }

        public float MaxHeight
        {
            get { float m = float.MinValue; foreach (float h in Heights) if (h > m) m = h; return m; }
        }

        public float At(int x, int z) => Heights[Mathf.Clamp(z, 0, Resolution - 1) * Resolution + Mathf.Clamp(x, 0, Resolution - 1)];

        /// <summary>Bilinear height at course coordinates (x east, y north).</summary>
        public float Sample(Vector2 p)
        {
            float fx = (p.x - OriginX) / Spacing, fz = (p.y - OriginZ) / Spacing;
            int x = Mathf.FloorToInt(fx), z = Mathf.FloorToInt(fz);
            float tx = fx - x, tz = fz - z;
            return Mathf.Lerp(Mathf.Lerp(At(x, z), At(x + 1, z), tx), Mathf.Lerp(At(x, z + 1), At(x + 1, z + 1), tx), tz);
        }

        public void Save(string path)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(0x31485A47); // "GZH1"
                w.Write(Resolution);
                w.Write(Size);
                w.Write(OriginX);
                w.Write(OriginZ);
                foreach (float h in Heights) w.Write(h);
            }
        }

        public static OsmHeightField Load(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (r.ReadInt32() != 0x31485A47) throw new InvalidDataException("Not a GolfSimZA height file");
                var f = new OsmHeightField { Resolution = r.ReadInt32(), Size = r.ReadSingle(), OriginX = r.ReadSingle(), OriginZ = r.ReadSingle() };
                if (f.Resolution < 33 || f.Resolution > 4097) throw new InvalidDataException("Bad height grid size " + f.Resolution);
                f.Heights = new float[f.Resolution * f.Resolution];
                byte[] raw = r.ReadBytes(f.Heights.Length * 4);
                if (raw.Length != f.Heights.Length * 4) throw new InvalidDataException("Height file is cut short");
                Buffer.BlockCopy(raw, 0, f.Heights, 0, raw.Length);
                return f;
            }
        }
    }

    /// <summary>Surface type per ground cell (surface.bin): Cells x Cells over the same square as the heights.</summary>
    public sealed class OsmSurfaceGrid
    {
        public int Cells;
        public float Size;
        public float OriginX, OriginZ;
        public byte[] Ids;

        public float CellSize => Size / Cells;

        public byte At(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - OriginX) / CellSize), z = Mathf.FloorToInt((p.y - OriginZ) / CellSize);
            if (x < 0 || z < 0 || x >= Cells || z >= Cells) return OsmSurface.Native;
            return Ids[z * Cells + x];
        }

        public void Save(string path)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(0x31535A47); // "GZS1"
                w.Write(Cells);
                w.Write(Size);
                w.Write(OriginX);
                w.Write(OriginZ);
                w.Write(Ids);
            }
        }

        public static OsmSurfaceGrid Load(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (r.ReadInt32() != 0x31535A47) throw new InvalidDataException("Not a GolfSimZA surface file");
                var g = new OsmSurfaceGrid { Cells = r.ReadInt32(), Size = r.ReadSingle(), OriginX = r.ReadSingle(), OriginZ = r.ReadSingle() };
                if (g.Cells < 32 || g.Cells > 8192) throw new InvalidDataException("Bad surface grid size " + g.Cells);
                g.Ids = r.ReadBytes(g.Cells * g.Cells);
                if (g.Ids.Length != g.Cells * g.Cells) throw new InvalidDataException("Surface file is cut short");
                return g;
            }
        }
    }
}
