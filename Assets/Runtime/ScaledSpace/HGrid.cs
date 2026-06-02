using System;
using System.Collections.Generic;
using SpaceStuff;

public class HGrid
{
    public struct GridCell : IEquatable<GridCell>
    {
        public int x;
        public int y;
        public int z;

        public GridCell(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(GridCell other)
        {
            return x == other.x &&
                y == other.y &&
                z == other.z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ")";
        }
    }

    private int maxLevels;
    private double[] levelCellSizes;
    private Dictionary<GridCell, List<DoubleRigidbody>>[] levels;

    public HGrid(int maxLevels, double baseCellSize, int scalingFactor)
    {
        this.maxLevels = maxLevels;
        levels = new Dictionary<GridCell, List<DoubleRigidbody>>[maxLevels];

        levelCellSizes = new double[maxLevels];
        levelCellSizes[0] = baseCellSize;
        for (int i = 0; i < maxLevels; i++)
        {
            if (i > 0)
                levelCellSizes[i] = levelCellSizes[i - 1] * scalingFactor;
            levels[i] = new Dictionary<GridCell, List<DoubleRigidbody>>();
        }
    }

    private int GetLevel(double radius)
    {
        double diameter = radius * 2.0;

        for (int i = 0; i < maxLevels; i++)
        {
            if (diameter <= levelCellSizes[i])
            {
                return i;
            }
        }
        return maxLevels - 1;
    }

    private GridCell GetCell(Vector3d pos, double cellSize)
    {
        return new GridCell(
            (int)Math.Floor(pos.x / cellSize),
            (int)Math.Floor(pos.y / cellSize),
            (int)Math.Floor(pos.z / cellSize)
        );
    }

    public void Clear()
    {
        for (int i = 0; i < maxLevels; i++)
            levels[i].Clear();
    }

    public void Insert(DoubleRigidbody rb)
    {
        if (rb.hGridLevel == -1)
            rb.hGridLevel = GetLevel(rb.GetCollisionRadius());

        double cellSize = levelCellSizes[rb.hGridLevel];

        GridCell cell = GetCell(
            rb.scaledTransform.realPosition,
            cellSize
        );

        if (!levels[rb.hGridLevel].TryGetValue(cell, out var list))
        {
            list = new List<DoubleRigidbody>();
            levels[rb.hGridLevel].Add(cell, list);
        }

        list.Add(rb);
    }
    
    public IEnumerable<(DoubleRigidbody, DoubleRigidbody)> GetCandidatePairs()
    {
        HashSet<(DoubleRigidbody, DoubleRigidbody)> pairs = new HashSet<(DoubleRigidbody, DoubleRigidbody)>();

        // Iterate through every occupied grid cell at every level
        for (int level = 0; level < maxLevels; level++)
        {
            foreach (var kvp in levels[level])
            {
                GridCell cell = kvp.Key;

                // All 27 cells in a 3x3x3 cube
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    GridCell neighbor = new GridCell(
                        cell.x + dx,
                        cell.y + dy,
                        cell.z + dz
                    );

                    if (!levels[level].TryGetValue(neighbor, out var otherList))
                        continue;

                    foreach (var a in kvp.Value)
                    foreach (var b in otherList)
                    {
                        // Prevent (a,a) and prevent duplicate pairs getting added e.g (a,b) and (b,a)
                        if (a.id == b.id || a.id > b.id)
                            continue;

                        pairs.Add((a, b));
                    }
                }
            }
        }

        return pairs;
    }
}