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

    private void Insert(DoubleRigidbody rb, int level, GridCell cell)
    {
        if(!levels[level].TryGetValue(cell, out var list))
        {
            list = new List<DoubleRigidbody>();
            levels[level].Add(cell, list);
        }
        list.Add(rb);
    }

    private void Remove(DoubleRigidbody rb, int level, GridCell cell)
    {
        if(!levels[level].TryGetValue(cell, out var list))
            return;
        list.Remove(rb);
    }

    public void Delete(DoubleRigidbody rb)
    {
        if (rb.currentColliderLevel == -1)
            return;
        Remove(rb, rb.currentColliderLevel, rb.currentColliderCell);
    }

    public void UpdatePosition(DoubleRigidbody rb)
    {
        // Effective radius = collider radius + distance traveled this frame.
        // This places fast-moving objects into a coarser level whose cell size
        // is large enough to contain the entire swept path in one cell.
        double displacement = rb.velocity.magnitude * Time.fixedDeltaTime;
        double effectiveRadius = rb.GetCollisionRadius() + displacement;
        int newLevel = GetLevel(effectiveRadius);

        if (rb.currentColliderLevel == -1)
        {
            rb.currentColliderLevel = newLevel;
            rb.currentColliderCell = GetCell(rb.scaledTransform.realPosition, levelCellSizes[newLevel]);
            Insert(rb, rb.currentColliderLevel, rb.currentColliderCell);
            return;
        }

        int prevLevel = rb.currentColliderLevel;
        GridCell prevCell = rb.currentColliderCell;
        GridCell newCell = GetCell(rb.scaledTransform.realPosition, levelCellSizes[newLevel]);

        // Re-insert only when the level or cell actually changes
        if (newLevel != prevLevel || !newCell.Equals(prevCell))
        {
            Remove(rb, prevLevel, prevCell);
            rb.currentColliderLevel = newLevel;
            rb.currentColliderCell = newCell;
            Insert(rb, newLevel, newCell);
        }
    }

    public void UpdateSize(DoubleRigidbody rb)
    {
        // Keep velocity inflation consistent with UpdatePosition
        double displacement = rb.velocity.magnitude * Time.fixedDeltaTime;
        int newLevel = GetLevel(rb.GetCollisionRadius() + displacement);
        int prevLevel = rb.currentColliderLevel;

        if (prevLevel == newLevel)
            return;

        if (prevLevel != -1)
            Remove(rb, prevLevel, rb.currentColliderCell);

        rb.currentColliderLevel = newLevel;
        rb.currentColliderCell = GetCell(rb.scaledTransform.realPosition, levelCellSizes[newLevel]);
        Insert(rb, rb.currentColliderLevel, rb.currentColliderCell);
    }

    public IEnumerable<DoubleRigidbody> GetCandidates(DoubleRigidbody rb)
    {
        if (rb.currentColliderLevel == -1)
        {
            double displacement = rb.velocity.magnitude * Time.fixedDeltaTime;
            rb.currentColliderLevel = GetLevel(rb.GetCollisionRadius() + displacement);
        }

        Vector3d pos = rb.scaledTransform.realPosition;

        // Check this level and above only — smaller objects do their own upward search,
        // so pairs are never missed and never double-checked at mismatched levels.
        for (int level = rb.currentColliderLevel; level < maxLevels; level++)
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

                foreach (DoubleRigidbody other in list)
                {
                    if (rb.id == other.id || rb.IsIgnoring(other.id) || other.IsIgnoring(rb.id))
                        continue;
                    yield return other;
                }
            }
        }
    }
}