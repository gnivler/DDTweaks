using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace DDTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class DDTweaks : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static ModSettings modSettings = new ();
    internal static Harmony harmony = new ("DDTweaks");
    
    private void Awake()
    {
        // Plugin startup logic
        Log = base.Logger;
        Log.LogWarning($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded successfully.");
        modSettings.books = Config.Bind("General", "Books usage scaling factor", 1f, "1 is vanilla.  0 is no books needed.  0.25 means only 25% of books are needed (75% reduction)");
        modSettings.tires = Config.Bind("General", "Tires wear scaling factor", 1f, "1 is vanilla, 0 is no wear.  0.25 means 25% tire wear (75% reduction)");
        modSettings.buyJunk = Config.Bind("General", "Show own current stock when buying junk", true,
            "Vanilla is false.  True will show you \"(have 25)\" of something when considering buying junk in bars");
        // modSettings.extraTraits = Config.Bind("General", "Extra trait slots", 0, "Anything over zero adds extra slots for traits");
        Patches.Patch();

    }
}