using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using App.Common;
using HarmonyLib;
using QxFramework.Core;
using QxFramework.Utilities;
using UnityEngine;
using UnityEngine.UI;

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
            {
                if (m.ChooseText.StartsWith("[Buy "))
                {
                    var match = Regex.Match(m.ChooseText, @"\d+\s(.+)]");
                    if (match.Success)
                    {
                        var itemName = match.Groups[1].Value;
                        var myCount = GameMgr.Get<IItemManager>()?.GetAllGoodsInCar()?.FirstOrDefault(g => g.Name == itemName)?.GoodsCout ?? 0;
                        // FileLog.Log($"Item Name: {itemName} (have {myCount})");
                        m.ChooseText = m.ChooseText.Replace("]", $" (have {myCount})]");
                    }
                }
            }
        }
        // Canvas/Layer 2/BarWindow(Clone)/BarWindow/ScrollRect/ViewPort/Content/BarPeopleItem(Clone)/Button/Text  Buy Junk
        // Canvas/Layer Event/NewEventUI(Clone)/Main/BG3/Scroll View/ViewPort/Content/NewEventChooseButton(Clone)  Buy 8 Compounds
    }
}