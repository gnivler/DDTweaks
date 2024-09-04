using UnityEngine;

namespace DDTweaks;

public static class Extensions
{
    public static Transform FindChildByName(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var result = FindChildByName(child, name);
            if (result)
                return result;
        }

        return null;
    }
}