using BepInEx.Configuration;

namespace DDTweaks;

public class ModSettings
{
    public ConfigEntry<float> books;
    public ConfigEntry<float> tires;

    public ConfigEntry<bool> buyJunk;
    public ConfigEntry<bool> easyClose;
    public ConfigEntry<bool> quickCombat;
    public ConfigEntry<bool> itemRarity;
    public ConfigEntry<bool> profitSliders;
    public ConfigEntry<bool> tradeDetails;
    // public ConfigEntry<bool> peopleView;
    public ConfigEntry<bool> reroll;
}