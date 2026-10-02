using System;
using System.Collections.Generic;

namespace NightPatrol.Run
{
    public static class TaskPicker
    {
        public static int RollCount(int min, int max, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            min = Math.Max(0, min);
            max = Math.Max(0, max);
            if (min > max) (min, max) = (max, min);
            return (int)(min + (long)(rng.NextDouble() * ((long)max - min + 1)));
        }

        public static List<T> Pick<T>(IList<T> candidates, int count, Func<T, bool> tryTake, Random rng)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (tryTake == null) throw new ArgumentNullException(nameof(tryTake));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var result = new List<T>();
            if (count <= 0) return result;
            var shuffled = new List<T>(candidates);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var seen = new HashSet<T>();
            foreach (var candidate in shuffled)
            {
                if (candidate is null || !seen.Add(candidate) || !tryTake(candidate)) continue;
                result.Add(candidate);
                if (result.Count == count) break;
            }
            return result;
        }
    }
}
