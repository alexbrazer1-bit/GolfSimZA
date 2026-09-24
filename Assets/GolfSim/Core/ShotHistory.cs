using System.Collections.Generic;

namespace GolfSimZA.Core
{
    public sealed class ShotHistory
    {
        private readonly List<ShotData> shots = new List<ShotData>();
        private readonly int capacity;

        public ShotHistory(int capacity = 20)
        {
            this.capacity = capacity;
        }

        public IReadOnlyList<ShotData> Shots => shots;
        public int Count => shots.Count;

        public void Add(ShotData shot)
        {
            if (!shot.IsValid)
                return;

            shots.Add(shot);
            if (shots.Count > capacity)
                shots.RemoveAt(0);
        }

        public void ReplaceLast(ShotData shot)
        {
            if (!shot.IsValid)
                return;

            if (shots.Count == 0)
            {
                Add(shot);
                return;
            }

            shots[shots.Count - 1] = shot;
        }

        public void Clear() => shots.Clear();
    }
}
