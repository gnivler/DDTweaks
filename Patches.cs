using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using App.Common;
using EventLogicSystem;
using HarmonyLib;
using QxFramework.Core;
using QxFramework.Utilities;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable InconsistentNaming

namespace DDTweaks;

public static class Patches
{
    public static void Patch()
    {
        DDTweaks.Log.LogWarning("Settings:");
        foreach (var field in typeof(ModSettings).GetFields())
        {
            var valueProperty = field.FieldType.GetProperty("Value");
            DDTweaks.Log.LogWarning($"{field.Name}: {valueProperty?.GetValue(field.GetValue(DDTweaks.modSettings))}");
        }

        if (DDTweaks.modSettings.books.Value < 1)
        {
            DDTweaks.harmony.PatchAll(typeof(Books.Init));
        }

        if (DDTweaks.modSettings.tires.Value < 1)
        {
            var orig = AccessTools.Method(typeof(Travel), "Init");
            var postfix = AccessTools.Method(typeof(Tires), nameof(Tires.InitPostfix));
            DDTweaks.harmony.Patch(orig, null, new HarmonyMethod(postfix));
        }

        if (DDTweaks.modSettings.buyJunk.Value)
        {
            DDTweaks.harmony.PatchAll(typeof(Junk));
        }

        if (DDTweaks.modSettings.itemRarity.Value)
        {
            DDTweaks.harmony.PatchAll(typeof(Rarity));
        }
    }

    internal static class Rarity
    {
        [HarmonyPatch(typeof(PeopleUpgradeManager), "UsePointToGetBuff")]
        [HarmonyPostfix]
        public static void Postfix(Personal b, int Book)
        {
            GameMgr.Get<IPeopleManager>().GetBuffSelections(b, new List<int>(), 10, out var dictionary);
            FileLog.Log($"[] ");
            foreach (var key in dictionary)
            {
                FileLog.Log($"][ {key.Key}: {key.Value}");
            }
        }

        // this is brittle and repetitive, but the game needs to be refactored
        private const string equipPattern = @"(.*?) \(<color=(#[A-Fa-f0-9]{6})>.*?</color>\)";
        private const string foodPattern = @"<color=(#[A-Fa-f0-9]{6})>\[.+\] (.+)<\/color>";
        private const string equipReplacement = "<color=$2>$1</color>";
        private const string foodReplacement = "<color=$1>$2</color>";

        private static readonly Dictionary<string, string[]> replacements = new Dictionary<string, string[]>
        {
            { "Equip", [equipPattern, equipReplacement] },
            { "Food", [foodPattern, foodReplacement] }
        };

        [HarmonyPatch(typeof(Equip), "Name", MethodType.Getter)]
        [HarmonyPostfix]
        public static void EquipNameGetterPostfix(Items __instance, ref string __result) => ChangeString(__instance, ref __result);

        [HarmonyPatch(typeof(Food), "FullName", MethodType.Getter)]
        [HarmonyPostfix]
        public static void FoodFullNameGetterPostfix(Items __instance, ref string __result) => ChangeString(__instance, ref __result);

        private static void ChangeString(Items __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
                return;

            var key = __instance.GetType().Name;
            if (replacements.TryGetValue(key, out _))
                __result = Regex.Replace(__result, replacements[key][0], replacements[key][1]);
        }
    }

    internal static class Books
    {
        internal static class Init
        {
            [HarmonyPatch(typeof(PeopleUpgradeManager), "Init")]
            public static void Postfix()
            {
                DDTweaks.harmony.Patch(AccessTools.Method(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.GetUpgradeBookUse)), null,
                    new HarmonyMethod(typeof(Books), nameof(GetUpgradeBookUsePostfix)));
                DDTweaks.harmony.PatchAll(typeof(Books));
            }
        }

        [HarmonyPatch(typeof(PeopleUpgradeManager))]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.RemoveBuffToGetPoint));
            yield return AccessTools.Method(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.UseBookToRemoveChosenPeopleBuff));
            yield return AccessTools.Method(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.UseBuffToGetPoint));
            yield return AccessTools.Method(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.UsePointToGetBuff));
            yield return AccessTools.Method(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.UsePointToRemoveBuff));
        }

        private static void Prefix(ref int Book) => Book = Convert.ToInt32(Book * DDTweaks.modSettings.books.Value);
        public static void GetUpgradeBookUsePostfix(ref int __result) => __result = Convert.ToInt32(__result * DDTweaks.modSettings.books.Value);
    }

    internal static class Tires
    {
        // just reruns what was done in the method, to add back the wear
        public static void StateChangePostfix()
        {
            var wear = MonoSingleton<Data>.Instance.Get<AutoData>()._autoMain.Speed / 60f;
            // FileLog.Log($">>> {wear} > {wear - wear * DDTweaks.modSettings.tires.Value}");
            foreach (var tires in MonoSingleton<Data>.Instance.Get<AutoData>()._autoMain.AutoTires)
            {
                tires.Characteristic["Distance"] -= wear - wear * DDTweaks.modSettings.tires.Value;
            }
        }

        internal static void InitPostfix()
        {
            var orig = AccessTools.Method(typeof(Travel), "StateChange");
            var postfix = AccessTools.Method(typeof(Tires), nameof(StateChangePostfix));
            DDTweaks.harmony.Patch(orig, null, new HarmonyMethod(postfix));
            DDTweaks.Log.LogInfo($"InitPostfix");
        }
    }

    internal static class Junk
    {
        [HarmonyPatch(typeof(NewEventUI), "OnDisplay")]
        public static void Prefix(object args)
        {
            var chooseMessage = (ChooseMessage)args;
            foreach (var m in chooseMessage.ChooseList)
                if (m.ChooseText.StartsWith("[Buy "))
                {
                    var match = Regex.Match(m.ChooseText, @"\d+\s(.+)]");
                    if (match.Success)
                    {
                        var itemName = match.Groups[1].Value;
                        var materials = MonoSingleton<Data>.Instance.Get<ItemData>().sources;
                        foreach (var i in materials)
                            if (itemName.StartsWith(i.Name))
                            {
                                m.ChooseText = m.ChooseText.Replace("]", $" (have {i.GoodsCout})]");
                                break;
                            }
                    }
                }
        }
        // Canvas/Layer 2/BarWindow(Clone)/BarWindow/ScrollRect/ViewPort/Content/BarPeopleItem(Clone)/Button/Text  Buy Junk
        // Canvas/Layer Event/NewEventUI(Clone)/Main/BG3/Scroll View/ViewPort/Content/NewEventChooseButton(Clone)  Buy 8 Compounds
    }
}