using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using App.Common;
using BehaviorDesigner.Runtime.Tasks.Basic.UnityGameObject;
using HarmonyLib;
using QxFramework.Core;
using QxFramework.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable InconsistentNaming

namespace DDTweaks;

public static class Patches
{
    internal static void Log(object message)
    {
        if (!DDTweaks.gnivler)
            return;
        DDTweaks.Log.LogWarning(message);
        FileLog.Log(message.ToString());
    }

    public static void Patch()
    {
        DDTweaks.Log.LogWarning("Settings:");
        foreach (var field in typeof(ModSettings).GetFields())
        {
            var valueProperty = field.FieldType.GetProperty("Value");
            DDTweaks.Log.LogWarning($"{field.Name}: {valueProperty?.GetValue(field.GetValue(DDTweaks.modSettings))}");
        }

        if (DDTweaks.modSettings.books.Value < 1)
            DDTweaks.harmony.PatchAll(typeof(Books.Init));

        if (DDTweaks.modSettings.tires.Value < 1)
        {
            var orig = AccessTools.Method(typeof(Travel), "Init");
            var postfix = AccessTools.Method(typeof(Tires), nameof(Tires.InitPostfix));
            DDTweaks.harmony.Patch(orig, null, new HarmonyMethod(postfix));
        }

        if (DDTweaks.modSettings.buyJunk.Value)
            DDTweaks.harmony.PatchAll(typeof(Junk));

        if (DDTweaks.modSettings.itemRarity.Value)
            DDTweaks.harmony.PatchAll(typeof(Rarity));

        if (DDTweaks.modSettings.profitSliders.Value)
            DDTweaks.harmony.PatchAll(typeof(ProfitSliders));

        if (File.Exists("C:\\SteamLibrary\\Dustland Delivery\\BepInEx\\plugins\\gnivler"))
        {
            DDTweaks.gnivler = true;
            Log("gnivler mode");
            DDTweaks.harmony.PatchAll(typeof(gnivler));
        }
    }

    internal static class gnivler
    {
        [HarmonyPatch(typeof(TradePanel), "OnDisplay")]
        public static void Postfix(TradePanel __instance)
        {
            var cloneSource = __instance.transform.Find("FractionWindow/OutPutBG").gameObject;
            var clone = Object.Instantiate(cloneSource, cloneSource.transform.parent.parent, true);
            clone.name = "NeedsSummary";
            var rt = clone.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350, 800);
            var text = clone.transform.Find("OutputDes").gameObject.GetComponent<Text>();
            text.name = "NeedsSummaryText";
            text.text = "Some Test Text\n" +
                        "Another line\n" +
                        "A third line that is much longer to break it";
            rt.Translate(100, 0, 0);
        }

        // private static void LogMessage(Type msgId, EventArgs param, object sender)
        // {
        //     FileLog.Log($"<> {msgId} from {sender}: {((HintMessage)param).Content}");
        // }

        // [HarmonyPatch(typeof(MessageQueue<HookType>), "DispatchMessage")]
        // [HarmonyPostfix]
        // public static void Postfix(MessageQueue<HookType> __instance, Type msgId, EventArgs param) => LogMessage(msgId, param);


        // [HarmonyPatch(typeof(MessageQueue<HintType>), "DispatchMessage")]
        // [HarmonyPostfix]
        // public static void Postfix(MessageQueue<HintType> __instance, object sender, Type msgId, EventArgs param) => LogMessage(msgId, param, sender);

        // [HarmonyPatch(typeof(MessageQueue<LocalizationMsg>), "DispatchMessage")]
        // [HarmonyPostfix]
        // public static void LocalizationMsgPostfix(MessageQueue<LocalizationMsg> __instance) => FileLog.Log("LocalizationMsg " + __instance);
        //
        // [HarmonyPatch(typeof(MessageQueue<RoadRegionMessageType>), "DispatchMessage")]
        // [HarmonyPostfix]
        // public static void RoadRegionMessageTypePostfix(MessageQueue<RoadRegionMessageType> __instance) => FileLog.Log("RoadRegionMessageType " + __instance);
        //
        // [HarmonyPatch(typeof(MessageQueue<LogType>), "DispatchMessage")]
        // [HarmonyPostfix]
        // public static void Postfix(MessageQueue<LogType> __instance) => FileLog.Log("LogType " + __instance);
        //
        // [HarmonyPatch(typeof(MessageQueue<WeatherMsg>), "DispatchMessage")]
        // [HarmonyPostfix]
        // public static void Postfix(MessageQueue<WeatherMsg> __instance) => FileLog.Log("WeatherMsg " + __instance);


        // the audio grinds on my neurons
        [HarmonyPatch(typeof(SoundMain), "SetState")]
        public static void Postfix(SoundMain __instance) => __instance.transform.Find("StateSound/Danger").GetComponent<AudioSource>().volume = 0;
    }

    internal static class ProfitSliders
    {
        private static readonly MethodInfo method = typeof(GameMgr).GetMethod("Get", []);
        private static readonly Type iCity = AccessTools.TypeByName("ICityManager");
        private static readonly MethodInfo iCityManagerGet = method?.MakeGenericMethod(iCity);
        private static readonly CityManager cityMgr = (CityManager)iCityManagerGet.Invoke(null, null);

        // if a 3rd becomes needed maybe look at writing a collection processor
        [HarmonyPatch(typeof(ShopWindowBase), "OpenPurchaseUI")]
        public static void Postfix(bool isBuy, int goodID)
        {
            var panel = (SliderUI)Object.FindObjectOfType<PurchasePanel>() ?? Object.FindObjectOfType<CaravanPurchasePanel>();
            var goods = (Goods)AccessTools.Field(panel.GetType(), "goods").GetValue(panel);
            Log($"{panel} {(isBuy ? "buying" : "selling")} {goods.Object?.Name}");
            SliderToBestPrice(panel, isBuy, goods);
        }

        private static void SliderToBestPrice(SliderUI panel, bool isBuy, Goods goods)
        {
            var slider = Object.FindObjectOfType<Slider>();
            int numToTrade = default, myStock = default, traderStock = default;
            float best = default;
            var itemMgr = GameMgr.Get<IItemManager>();
            var marketMgr = GameMgr.Get<IMarketManager>();
            CaravanTradeArgs caravanTradeArgs = default;
            switch (panel)
            {
                case PurchasePanel:
                    myStock = itemMgr.GetGoodsCout(goods.Object.Id);
                    traderStock = goods.Count;
                    break;
                case CaravanPurchasePanel caravanPurchasePanel:
                    caravanTradeArgs = caravanPurchasePanel.Args;
                    var goodsStore = (GoodsStore)caravanTradeArgs.selfCity.Modules.Find(m => m is GoodsStore);
                    myStock = goodsStore.storeData.GetItemCount(goods.Object.Id);
                    traderStock = marketMgr.GetMarket(caravanTradeArgs.tradeCity.CityId, caravanTradeArgs.mapID).GoodsList.Find(g => g.Id == goods.Id).Count;
                    break;
            }

            var availableStock = isBuy ? traderStock : myStock;
            Log($"{(isBuy ? "buying" : "selling")} {goods.Object?.Name} with available stock at {availableStock}");
            for (var i = 1; i <= availableStock; i++)
            {
                var stock = isBuy ? goods.Count - i + 1 : goods.Count + i - 1;
                float adjPrice = default;
                switch (panel)
                {
                    case PurchasePanel:
                        adjPrice = marketMgr.GetPrice(goods.Id, stock, isBuy ? 1 : -1, isBuy);
                        break;
                    case CaravanPurchasePanel purchasePanel:
                    {
                        adjPrice = GameMgr.Get<IMarketManager>()
                            .GetPrice(caravanTradeArgs.tradeCity.CityId, caravanTradeArgs.mapID, goods.Id, goods.Count, isBuy ? 1 * i : -1 * i, isBuy);
                        break;
                    }
                }

                var unitCost = (float)AccessTools.PropertyGetter(panel.GetType(), "SinglePiece").Invoke(panel, []);
                var valToCompare = isBuy ? Math.Max(adjPrice, unitCost) : Math.Min(adjPrice, unitCost);
                float profit;
                if (isBuy)
                    profit = (goods.Price - valToCompare) * i;
                else
                    profit = (valToCompare - goods.Price) * i;
                profit = Convert.ToInt32(profit);
                // the base game isn't properly rounding the Profit it displays on the UI currently so the slider
                // can appear to be 1 off from best but in fact this result is more correct than the UI displayed value.  nominal difference in any case
                Log($"Volume: {i}/{availableStock} => adjPrice: {adjPrice}, Profit: {profit}");
                if (profit < 0 // low-stock goods come up "red" profit immediately
                    || (adjPrice < unitCost && isBuy)
                    || (adjPrice > unitCost && !isBuy)
                    || profit < best)
                {
                    Log(
                        $"Break: profit < 0: {profit < 0} (adjPrice < unitCost && isBuy): {adjPrice < unitCost && isBuy} (adjPrice > unitCost && !isBuy): {adjPrice > unitCost && !isBuy} profit < best: {profit < best} (unitCost {unitCost})");
                    break;
                }

                best = profit;
                numToTrade = i;
            }

            if (best > 0)
            {
                var percent = (float)numToTrade / availableStock;
                Log($"{goods.Object?.Name} slider percent: {percent}");
                slider.SetValueWithoutNotify(percent);
            }
        }
    }

    internal static class Rarity
    {
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