using System.Collections.Generic;
using UnityEngine;

public static class PlatformController
{
    public static List<PlatformUnit> platforms = new();

    public static void Register(PlatformUnit platform)
    {
        if (!platforms.Contains(platform))
            platforms.Add(platform);
    }
}
