using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace DDTweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class DDTweaks : BaseUnityPlugin
{
    internal static bool gnivler;
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
        modSettings.buyJunk = Config.Bind("General", "Show own stock", true, "Vanilla is false.  True will show you \"(have 25)\" of something when considering buying junk");
        modSettings.easyClose = Config.Bind("General", "Allow easy closing of windows", true, "Allow closing of windows, with right-click for top-most and Esc to close all at once");
        modSettings.quickCombat = Config.Bind("General", "Enter to fight", true, "Executes a coup-de-gras if it's safe, otherwise does a full volley if you have the ammo");
        modSettings.itemRarity = Config.Bind("General", "Item qualities", true, "Superior equipment and food strings are themselves coloured instead of tagged with a modifier word that is coloured");
        modSettings.profitSliders = Config.Bind("General", "Profit sliders", true, "Trading sliders will try to jump to the most potential profit");
        modSettings.tradeDetails = Config.Bind("General", "Show trade detail panel", true, "An extra panel with a summary of the production and demands of existing routes is shown");
        // modSettings.peopleView = Config.Bind("General", "People shown by name", true,
            // "Instead of the class being prominent, it's the name of the person, and on the main UI, it shows their name not their level");
        modSettings.reroll = Config.Bind("General", "Re-roll trait picks", true, "Press backspace to re-roll the 3 skills available to pick when spending trait points");
        Patches.Patch();
        if (modSettings.easyClose.Value)
            gameObject.AddComponent<EasyCloser>();
        if (modSettings.quickCombat.Value)
            gameObject.AddComponent<QuickCombat>();
    }

    private void Update()
    {
        // if (Input.GetKeyDown(KeyCode.Backslash))
        //     GameMgr.Get<IItemManager>().GetGoods(1002).GoodsCout += 500;
        //
        if (!gnivler)
            return;

        // if (Input.GetKeyDown(KeyCode.Backspace))
        // {
        // Singleton<MessageManager>.Instance._queues.Do(x => FileLog.Log($"** {x.Key} {x.Value}"));
        // var people = GameMgr.Get<IPeopleManager>().Getpeople();
        // people.AllPeople.Do(x => Patches.Log($"<> {x.Name} {x.RealName} {x.PeopleID}"));
        //     
    }
}