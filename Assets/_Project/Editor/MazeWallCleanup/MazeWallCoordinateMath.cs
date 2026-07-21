using System;
using System.Collections.Generic;

namespace MazeWallCleanup
{
    internal enum MazeWallAxis
    {
        Horizontal,
        Vertical
    }

    internal readonly struct MazeWallCoordinates
    {
        public MazeWallCoordinates(int id, MazeWallAxis axis, float line, float start, float end)
        {
            Id = id;
            Axis = axis;
            Line = line;
            Start = Math.Min(start, end);
            End = Math.Max(start, end);
        }

        public int Id { get; }
        public MazeWallAxis Axis { get; }
        public float Line { get; }
        public float Start { get; }
        public float End { get; }
    }

    internal readonly struct MazeWallTarget
    {
        public MazeWallTarget(int id, MazeWallAxis axis, float line, float start, float end)
        {
            Id = id;
            Axis = axis;
            Line = line;
            Start = Math.Min(start, end);
            End = Math.Max(start, end);
        }

        public int Id { get; }
        public MazeWallAxis Axis { get; }
        public float Line { get; }
        public float Start { get; }
        public float End { get; }
        public float Length => End - Start;
        public float Center => (Start + End) * 0.5f;
    }

    internal static class MazeWallCoordinateMath
    {
        enum CoordinateRole
        {
            Line,
            Start,
            End
        }

        readonly struct CoordinateReference
        {
            public CoordinateReference(int wallIndex, CoordinateRole role, float value)
            {
                WallIndex = wallIndex;
                Role = role;
                Value = value;
            }

            public int WallIndex { get; }
            public CoordinateRole Role { get; }
            public float Value { get; }
        }

        sealed class MutableTarget
        {
            public int Id;
            public MazeWallAxis Axis;
            public float Line;
            public float Start;
            public float End;
        }

        public static IReadOnlyList<MazeWallTarget> BuildTargets(
            IReadOnlyList<MazeWallCoordinates> walls,
            float tolerance)
        {
            if (walls == null)
            {
                throw new ArgumentNullException(nameof(walls));
            }

            if (tolerance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance cannot be negative.");
            }

            List<MutableTarget> targets = new List<MutableTarget>(walls.Count);
            List<CoordinateReference> xCoordinates = new List<CoordinateReference>(walls.Count * 2);
            List<CoordinateReference> zCoordinates = new List<CoordinateReference>(walls.Count * 2);

            for (int index = 0; index < walls.Count; index++)
            {
                MazeWallCoordinates wall = walls[index];
                targets.Add(new MutableTarget
                {
                    Id = wall.Id,
                    Axis = wall.Axis,
                    Line = wall.Line,
                    Start = wall.Start,
                    End = wall.End
                });

                if (wall.Axis == MazeWallAxis.Horizontal)
                {
                    xCoordinates.Add(new CoordinateReference(index, CoordinateRole.Start, wall.Start));
                    xCoordinates.Add(new CoordinateReference(index, CoordinateRole.End, wall.End));
                    zCoordinates.Add(new CoordinateReference(index, CoordinateRole.Line, wall.Line));
                }
                else
                {
                    xCoordinates.Add(new CoordinateReference(index, CoordinateRole.Line, wall.Line));
                    zCoordinates.Add(new CoordinateReference(index, CoordinateRole.Start, wall.Start));
                    zCoordinates.Add(new CoordinateReference(index, CoordinateRole.End, wall.End));
                }
            }

            ClusterAndAssign(xCoordinates, targets, tolerance);
            ClusterAndAssign(zCoordinates, targets, tolerance);

            List<MazeWallTarget> result = new List<MazeWallTarget>(targets.Count);
            foreach (MutableTarget target in targets)
            {
                result.Add(new MazeWallTarget(
                    target.Id,
                    target.Axis,
                    target.Line,
                    target.Start,
                    target.End));
            }

            return result;
        }

        static void ClusterAndAssign(
            List<CoordinateReference> coordinates,
            List<MutableTarget> targets,
            float tolerance)
        {
            coordinates.Sort((left, right) => left.Value.CompareTo(right.Value));
            int clusterStart = 0;

            while (clusterStart < coordinates.Count)
            {
                int clusterEnd = clusterStart;
                while (clusterEnd + 1 < coordinates.Count
                    && coordinates[clusterEnd + 1].Value - coordinates[clusterEnd].Value <= tolerance)
                {
                    clusterEnd++;
                }

                float canonical = Median(coordinates, clusterStart, clusterEnd);
                for (int index = clusterStart; index <= clusterEnd; index++)
                {
                    Assign(targets[coordinates[index].WallIndex], coordinates[index].Role, canonical);
                }

                clusterStart = clusterEnd + 1;
            }
        }

        static float Median(List<CoordinateReference> sorted, int start, int end)
        {
            int count = end - start + 1;
            int middle = start + count / 2;
            if ((count & 1) == 1)
            {
                return sorted[middle].Value;
            }

            return (sorted[middle - 1].Value + sorted[middle].Value) * 0.5f;
        }

        static void Assign(MutableTarget target, CoordinateRole role, float value)
        {
            switch (role)
            {
                case CoordinateRole.Line:
                    target.Line = value;
                    break;
                case CoordinateRole.Start:
                    target.Start = value;
                    break;
                case CoordinateRole.End:
                    target.End = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }
        }
    }
}
