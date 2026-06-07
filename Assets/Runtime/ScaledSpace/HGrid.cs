using System;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

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
            return x == other.x && y == other.y && z == other.z;
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
    private Dictionary<GridCell, List<ScaledCollider>>[] levels;

    public HGrid(int maxLevels, double baseCellSize, int scalingFactor)
    {
        this.maxLevels = maxLevels;
        levels = new Dictionary<GridCell, List<ScaledCollider>>[maxLevels];

        levelCellSizes = new double[maxLevels];
        levelCellSizes[0] = baseCellSize;
        for (int i = 0; i < maxLevels; i++)
        {
            if (i > 0)
                levelCellSizes[i] = levelCellSizes[i - 1] * scalingFactor;
            levels[i] = new Dictionary<GridCell, List<ScaledCollider>>();
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

    private void Insert(ScaledCollider collider, int level, GridCell cell)
    {
        if(!levels[level].TryGetValue(cell, out var list))
        {
            list = new List<ScaledCollider>();
            levels[level].Add(cell, list);
        }
        list.Add(collider);
    }

    private void Remove(ScaledCollider collider, int level, GridCell cell)
    {
        if(!levels[level].TryGetValue(cell, out var list))
            return;
        list.Remove(collider);
    }

    public void Delete(ScaledCollider collider)
    {
        if (collider.hGridLevel == -1)
            return;
        Remove(collider, collider.hGridLevel, collider.hGridCell);
    }

    public void UpdatePosition(ScaledCollider collider)
    {
        // Effective radius = collider radius + distance traveled this frame.
        // This places fast-moving objects into a coarser level whose cell size
        // is large enough to contain the entire swept path in one cell.
        double displacement = collider.doubleRigidbody.velocity.magnitude * Time.fixedDeltaTime;
        double effectiveRadius = collider.GetRadius() + displacement;
        int newLevel = GetLevel(effectiveRadius);

        if (collider.hGridLevel == -1)
        {
            collider.hGridLevel = newLevel;
            collider.hGridCell = GetCell(collider.GetRealCenter(), levelCellSizes[newLevel]);
            Insert(collider, collider.hGridLevel, collider.hGridCell);
            return;
        }

        int prevLevel = collider.hGridLevel;
        GridCell prevCell = collider.hGridCell;
        GridCell newCell = GetCell(collider.GetRealCenter(), levelCellSizes[newLevel]);

        // Re-insert only when the level or cell actually changes
        if (newLevel != prevLevel || !newCell.Equals(prevCell))
        {
            Remove(collider, prevLevel, prevCell);
            collider.hGridLevel = newLevel;
            collider.hGridCell = newCell;
            Insert(collider, newLevel, newCell);
        }
    }

    public void UpdateSize(ScaledCollider collider)
    {
        // Keep velocity inflation consistent with UpdatePosition
        double displacement = collider.doubleRigidbody.velocity.magnitude * Time.fixedDeltaTime;
        int newLevel = GetLevel(collider.GetRadius() + displacement);
        int prevLevel = collider.hGridLevel;

        if (prevLevel == newLevel)
            return;

        if (prevLevel != -1)
            Remove(collider, prevLevel, collider.hGridCell);

        collider.hGridLevel = newLevel;
        collider.hGridCell = GetCell(collider.GetRealCenter(), levelCellSizes[newLevel]);
        Insert(collider, collider.hGridLevel, collider.hGridCell);
    }

    public IEnumerable<ScaledCollider> GetCandidates(ScaledCollider collider)
    {
        if (collider.hGridLevel == -1)
        {
            double displacement = collider.doubleRigidbody.velocity.magnitude * Time.fixedDeltaTime;
            collider.hGridLevel = GetLevel(collider.GetRadius() + displacement);
        }

        Vector3d pos = collider.GetRealCenter();

        // Check this level and above only — smaller objects do their own upward search,
        // so pairs are never missed and never double-checked at mismatched levels.
        for (int level = collider.hGridLevel; level < maxLevels; level++)
        {
            GridCell center = GetCell(pos, levelCellSizes[level]);

            // 3×3×3 neighborhood is sufficient: the inflation guarantee means a fast
            // object's prevPos and realPos both map to the same cell or one neighbor
            // at that object's level (displacement ≤ cellSize/2 by GetLevel's invariant).
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                GridCell cell = new GridCell(center.x + dx, center.y + dy, center.z + dz);
                if (!levels[level].TryGetValue(cell, out var list))
                    continue;

                foreach (ScaledCollider other in list)
                {
                    if (collider.id == other.id || collider.IsIgnoring(other.id) || other.IsIgnoring(collider.id))
                        continue;
                    yield return other;
                }
            }
        }
    }
}