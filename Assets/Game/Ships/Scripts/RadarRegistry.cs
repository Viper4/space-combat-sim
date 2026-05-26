using System.Collections.Generic;

public static class RadarRegistry
{
    static uint nextId;

    static readonly Dictionary<uint, RadarTarget> targets = new();

    public static uint Register(RadarTarget target)
    {
        uint id = nextId++;
        targets[id] = target;

        return id;
    }

    public static void Unregister(uint id)
    {
        targets.Remove(id);
    }

    public static bool TryGet(uint id, out RadarTarget target)
    {
        return targets.TryGetValue(id, out target);
    }

    public static void Clear()
    {
        targets.Clear();
        nextId = 0;
    }
}