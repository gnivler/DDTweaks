using System.Diagnostics;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace DDTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class DDTweaks : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static readonly ModSettings modSettings = new();
    internal static readonly Harmony harmony = new("DDTweaks");

    private void Awake()
    {
        // Plugin startup logic
        Log = base.Logger;
        Log.LogWarning($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded successfully.");
        modSettings.books = Config.Bind("General", "Books usage scaling factor", 1f, "1 is vanilla.  0 is no books needed.  0.25 means only 25% of books are needed (75% reduction)");
        modSettings.tires = Config.Bind("General", "Tires wear scaling factor", 1f, "1 is vanilla, 0 is no wear.  0.25 means 25% tire wear (75% reduction)");
        modSettings.buyJunk = Config.Bind("General", "Show own stock", true, "Vanilla is false.  True will show you \"(have 25)\" of something when considering buying junk in bars");
        modSettings.easyClose = Config.Bind("General", "Allow easy closing of windows", true, "Allow closing of windows, with right-click for top-most and Esc to close all at once");
        modSettings.quickCombat = Config.Bind("General", "Enter to fight", true, "Executes a coup-de-gras if it's safe, otherwise does a full volley if you have the ammo");
        Patches.Patch();
        if (modSettings.easyClose.Value)
            gameObject.AddComponent<EasyCloser>();
        if (modSettings.quickCombat.Value)
            gameObject.AddComponent<QuickCombat>();
    }
}