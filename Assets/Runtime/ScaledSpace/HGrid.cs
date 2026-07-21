using System;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

public class HGrid
{
    public struct GridCell : IEquatable<GridCell>
    {
        public int level;
        public int x;
        public int y;
        public int z;

        public GridCell(int level, int x, int y, int z)
        {
            this.level = level;
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(GridCell other)
        {
            return level == other.level && x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(level, x, y, z);
        }

        public override string ToString()
        {
            return level + " (" + x + ", " + y + ", " + z + ")";
        }
    }

    private int maxLevels;
    private double[] levelCellSizes;
    private Dictionary<GridCell, List<ScaledCollider>> grids;

    public HGrid(int maxLevels, double baseCellSize, int scalingFactor)
    {
        this.maxLevels = maxLevels;
        grids = new Dictionary<GridCell, List<ScaledCollider>>();

        levelCellSizes = new double[maxLevels];
        levelCellSizes[0] = baseCellSize;
        for (int i = 0; i < maxLevels; i++)
        {
            if (i > 0)
                levelCellSizes[i] = levelCellSizes[i - 1] * scalingFactor;
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

    private GridCell GetCell(Vector3d pos, int level)
    {
        double cellSize = levelCellSizes[level];
        return new GridCell(
            level,
            (int)Math.Floor(pos.x / cellSize),
            (int)Math.Floor(pos.y / cellSize),
            (int)Math.Floor(pos.z / cellSize)
        );
    }

    public void Clear()
    {
        grids.Clear();
    }

    private void Insert(ScaledCollider collider, GridCell cell)
    {
        if(!grids.TryGetValue(cell, out var list))
        {
            list = new List<ScaledCollider>();
            grids.Add(cell, list);
        }
        collider.listIndex = list.Count;
        list.Add(collider);
    }

    private void Remove(ScaledCollider collider, GridCell cell)
    {
        if(!grids.TryGetValue(cell, out var list))
        {
            Debug.LogWarning($"[HGrid] Failed to remove {collider.id} {collider.name}");
            return;
        }
        if (list.Count <= 0)
            return; // Already removed it
        int lastIndex = list.Count - 1;
        ScaledCollider last = list[lastIndex];

        // Replace with collider at end of list for fast removal and maintain indices
        list[collider.listIndex] = last;
        last.listIndex = collider.listIndex;
        list.RemoveAt(lastIndex);
    }

    public void Delete(ScaledCollider collider)
    {
        if (collider.listIndex == -1)
            return;
        Remove(collider, collider.hGridCell);
        collider.listIndex = -1;
    }

    public void UpdatePosition(ScaledCollider collider)
    {
        // Effective radius = collider radius + distance traveled this frame.
        // This places fast-moving objects into a coarser level whose cell size
        // is large enough to contain the entire swept path in one cell.
        double displacement = collider.scaledRigidbody.velocity.magnitude * Time.fixedDeltaTime;
        double effectiveRadius = collider.GetRadius() + displacement;
        int newLevel = GetLevel(effectiveRadius);

        if (collider.listIndex == -1)
        {
            collider.hGridCell = GetCell(collider.GetRealCenter(), newLevel);
            Insert(collider, collider.hGridCell);
            return;
        }

        GridCell prevCell = collider.hGridCell;
        GridCell newCell = GetCell(collider.GetRealCenter(), newLevel);

        // Re-insert only when the level or cell actually changes
        if (!newCell.Equals(prevCell))
        {
            Remove(collider, prevCell);
            collider.hGridCell = newCell;
            Insert(collider, newCell);
        }
    }

    public void UpdateSize(ScaledCollider collider)
    {
        // Keep velocity inflation consistent with UpdatePosition
        double displacement = collider.scaledRigidbody.velocity.magnitude * Time.fixedDeltaTime;
        double effectiveRadius = collider.GetRadius() + displacement;
        int newLevel = GetLevel(effectiveRadius);
        int prevLevel = collider.hGridCell.level;

        if (prevLevel == newLevel)
            return;

        if (collider.listIndex != -1)
            Remove(collider, collider.hGridCell);

        collider.hGridCell = GetCell(collider.GetRealCenter(), newLevel);
        Insert(collider, collider.hGridCell);
    }

    public IEnumerable<ScaledCollider> GetCandidates(ScaledCollider collider)
    {
        int baseLevel = collider.hGridCell.level;
        if (collider.listIndex == -1)
        {
            double displacement = collider.scaledRigidbody.velocity.magnitude * Time.fixedDeltaTime;
            double effectiveRadius = collider.GetRadius() + displacement;
            baseLevel = GetLevel(effectiveRadius);
        }

        Vector3d pos = collider.GetRealCenter();

        // Check this level and above only — smaller objects do their own upward search,
        // so pairs are never missed and never double-checked at mismatched levels.
        for (int level = baseLevel; level < maxLevels; level++)
        {
            GridCell center = GetCell(pos, level);
            // 3×3×3 neighborhood is sufficient: the inflation guarantee means a fast
            // object's prevPos and realPos both map to the same cell or one neighbor
            // at that object's level (displacement ≤ cellSize/2 by GetLevel's invariant).
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                GridCell cell = new GridCell(level, center.x + dx, center.y + dy, center.z + dz);
                if (!grids.TryGetValue(cell, out var list))
                    continue;

                foreach (ScaledCollider other in list)
                {
                    if (other == null || collider.id == other.id || collider.IsIgnoring(other.id) || other.IsIgnoring(collider.id))
                        continue;
                    yield return other;
                }
            }
        }
    }

    public IEnumerable<ScaledCollider> GetOverlapCandidates(Vector3d position, double radius)
    {
        for (int level = 0; level < maxLevels; level++)
        {
            double cellSize = levelCellSizes[level];

            // Largest collider radius that could exist at this level.
            double maxColliderRadius = cellSize * 0.5;

            // Get min and max positions of biggest possible spheres at this level that could overlap this sphere with position and radius
            // AABB stretched over this space
            Vector3d min = position - Vector3d.one * (radius + maxColliderRadius);
            Vector3d max = position + Vector3d.one * (radius + maxColliderRadius);

            GridCell minCell = GetCell(min, level);
            GridCell maxCell = GetCell(max, level);

            for (int x = minCell.x; x <= maxCell.x; x++)
            for (int y = minCell.y; y <= maxCell.y; y++)
            for (int z = minCell.z; z <= maxCell.z; z++)
            {
                GridCell cell = new GridCell(level, x, y, z);

                if (!grids.TryGetValue(cell, out var list))
                    continue;

                foreach (ScaledCollider collider in list)
                {
                    if (collider == null)
                        continue;
                    yield return collider;
                }
            }
        }
    }

    private static bool RaycastSphere(Vector3d origin, Vector3d direction, Vector3d center, double radius, out double distance, out Vector3d point, out Vector3d normal)
    {
        distance = 0.0;
        point = default;
        normal = default;

        Vector3d toSphere = center - origin;

        double projection = Vector3d.Dot(toSphere, direction);

        // Sphere is entirely behind the ray.
        if (projection < 0.0)
            return false;

        double sqrDistanceToRay = toSphere.sqrMagnitude - projection * projection;

        double radiusSq = radius * radius;

        if (sqrDistanceToRay > radiusSq)
            return false;

        double offset = Math.Sqrt(radiusSq - sqrDistanceToRay);

        double t = projection - offset;

        // Ray origin is inside the sphere.
        if (t < 0.0)
            t = projection + offset;

        distance = t;

        point = origin + direction * distance;

        normal = (point - center).normalized;

        return true;
    }

    private void RaycastLevel(Vector3d origin, Vector3d direction, double maxDistance, int level, HashSet<uint> testedColliders, ref double closestDistance, ref ScaledCollider closestCollider, ref Vector3d closestPoint, ref Vector3d closestNormal)
    {
        double cellSize = levelCellSizes[level];

        GridCell cell = GetCell(origin, level);

        // Direction signs
        int stepX = direction.x >= 0.0 ? 1 : -1;
        int stepY = direction.y >= 0.0 ? 1 : -1;
        int stepZ = direction.z >= 0.0 ? 1 : -1;

        double nextBoundaryX = direction.x >= 0.0 ? (cell.x + 1) * cellSize : cell.x * cellSize;

        double nextBoundaryY = direction.y >= 0.0 ? (cell.y + 1) * cellSize : cell.y * cellSize;

        double nextBoundaryZ = direction.z >= 0.0 ? (cell.z + 1) * cellSize : cell.z * cellSize;

        double tMaxX = Math.Abs(direction.x) > double.Epsilon ? (nextBoundaryX - origin.x) / direction.x : double.PositiveInfinity;

        double tMaxY = Math.Abs(direction.y) > double.Epsilon ? (nextBoundaryY - origin.y) / direction.y : double.PositiveInfinity;

        double tMaxZ = Math.Abs(direction.z) > double.Epsilon ? (nextBoundaryZ - origin.z) / direction.z : double.PositiveInfinity;

        double tDeltaX = Math.Abs(direction.x) > double.Epsilon ? cellSize / Math.Abs(direction.x) : double.PositiveInfinity;

        double tDeltaY = Math.Abs(direction.y) > double.Epsilon ? cellSize / Math.Abs(direction.y) : double.PositiveInfinity;

        double tDeltaZ = Math.Abs(direction.z) > double.Epsilon ? cellSize / Math.Abs(direction.z) : double.PositiveInfinity;

        while (true)
        {
            GridCell currentCell = new GridCell(level, cell.x, cell.y, cell.z);

            if (grids.TryGetValue(currentCell, out List<ScaledCollider> list))
            {
                foreach (ScaledCollider collider in list)
                {
                    if (collider == null)
                        continue;

                    if (testedColliders.Contains(collider.id))
                        continue;

                    testedColliders.Add(collider.id);

                    if (!RaycastSphere(
                            origin,
                            direction,
                            collider.GetRealCenter(),
                            collider.GetRadius(),
                            out double distance,
                            out Vector3d point,
                            out Vector3d normal))
                    {
                        continue;
                    }

                    if (distance < 0.0 ||
                        distance > maxDistance ||
                        distance >= closestDistance)
                    {
                        continue;
                    }

                    closestDistance = distance;
                    closestCollider = collider;
                    closestPoint = point;
                    closestNormal = normal;
                }
            }

            // The closest hit already found is before the next cell.
            double nextT =
                Math.Min(tMaxX, Math.Min(tMaxY, tMaxZ));

            if (nextT > maxDistance ||
                nextT > closestDistance)
            {
                break;
            }

            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                cell.x += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxZ)
            {
                cell.y += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                cell.z += stepZ;
                tMaxZ += tDeltaZ;
            }
        }
    }

    public bool Raycast(Vector3d origin, Vector3d direction, double maxDistance, out ScaledRaycastHit hit)
    {
        hit = default;

        if (direction.sqrMagnitude <= double.Epsilon)
            return false;
        
        double closestDistance = maxDistance;
        ScaledCollider closestCollider = null;
        Vector3d closestPoint = default;
        Vector3d closestNormal = default;

        HashSet<uint> testedColliders = new HashSet<uint>();

        for (int level = 0; level < maxLevels; level++)
        {
            RaycastLevel(origin, direction, maxDistance, level, testedColliders, ref closestDistance, ref closestCollider, ref closestPoint, ref closestNormal);
        }

        if (closestCollider == null)
            return false;
        
        hit = new ScaledRaycastHit
        {
            collider = closestCollider,
            point = closestPoint,
            normal = closestNormal,
            distance = closestDistance
        };

        return true;
    }

    public struct ScaledRaycastHit
    {
        public ScaledCollider collider;
        public Vector3d point;
        public Vector3d normal;
        public double distance;
    }
}