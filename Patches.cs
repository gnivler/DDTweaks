using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using App.Common;
using HarmonyLib;
using QxFramework.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
            DDTweaks.Log.LogWarning("gnivler mode");
            DDTweaks.harmony.PatchAll(typeof(gnivler));
        }
    }

    internal static class gnivler
    {
        // the audio grinds on my neurons
        [HarmonyPatch(typeof(SoundMain), "SetState")]
        public static void Postfix(SoundMain __instance) => __instance.transform.Find("StateSound/Danger").GetComponent<AudioSource>().volume = 0;
    }

    internal static class ProfitSliders
    {
        private static readonly MethodInfo method = typeof(GameMgr).GetMethod("Get", []);
        private static readonly Type iCity = AccessTools.TypeByName("ICityManager");
        private static readonly MethodInfo genericMethod = method?.MakeGenericMethod(iCity);
        private static readonly CityManager cityMgr = (CityManager)genericMethod.Invoke(null, null);


        // if a 3rd becomes needed maybe look at writing a collection processor
        [HarmonyPatch(typeof(PurchasePanel), "OnDisplay")]
        public static void Postfix(PurchasePanel __instance, bool ___isBuy, Goods ___goods)
        {
            var piece = (float)AccessTools.PropertyGetter(typeof(PurchasePanel), "SinglePiece").Invoke(__instance, []);
            SliderToBestPrice(__instance, ___isBuy, ___goods, piece);
        }

        [HarmonyPatch(typeof(CaravanPurchasePanel), "OnDisplay")]
        public static void Postfix(CaravanPurchasePanel __instance, bool ___isBuy, Goods ___goods)
        {
            var piece = (float)AccessTools.PropertyGetter(typeof(CaravanPurchasePanel), "SinglePiece").Invoke(__instance, []);
            SliderToBestPrice(__instance, ___isBuy, ___goods, piece);
        }

        private static void SliderToBestPrice(SliderUI panel, bool isBuy, Goods goods, float unitCost)
        {
            var slider = Object.FindObjectOfType<Slider>();
            int numToTrade = default, myStock = default, traderStock = default;
            float best = default;
            var itemMgr = GameMgr.Get<IItemManager>();
            var marketMgr = GameMgr.Get<IMarketManager>();

            switch (panel)
            {
                case PurchasePanel:
                    myStock = itemMgr.GetGoodsCout(goods.Object.Id);
                    traderStock = goods.Count;
                    break;
                case CaravanPurchasePanel:
                    var caravanArgs = (CaravanTradeArgs)AccessTools.Field(typeof(CaravanPurchasePanel), "Args").GetValue(panel);
                    var goodsStore = (GoodsStore)caravanArgs.selfCity.Modules.Find(m => m is GoodsStore);
                    myStock = goodsStore.storeData.GetItemCount(goods.Object.Id);
                    var goodsList = marketMgr.GetMarket(caravanArgs.tradeCity.CityId, caravanArgs.mapID).GoodsList;
                    DDTweaks.Log.LogWarning($"GoodsList? {goodsList}, goods.Id: {goods.Id}");
                    traderStock = marketMgr.GetMarket(caravanArgs.tradeCity.CityId, caravanArgs.mapID).GoodsList.Find(g => g.Id == goods.Id).Count;
                    break;
            }

            var howManyGoodsToCheck = isBuy ? traderStock : myStock;
            for (var i = 1; i <= howManyGoodsToCheck; i++)
            {
                var stock = isBuy ? goods.Count - i + 1 : goods.Count + i - 1;
                var price = marketMgr.GetPrice(goods.Id, stock, isBuy ? 1 : -1, isBuy);
                // DDTweaks.Log.LogWarning($"GetPrice returned {price} for {i} {goods} with city stock at {stock} (Buy? {isBuy})");
                var valToCompare = isBuy ? Math.Max(price, unitCost) : Math.Min(price, unitCost);
                float profit;
                if (isBuy)
                    profit = (goods.Price - valToCompare) * i;
                else
                    profit = (valToCompare - goods.Price) * i;
                if (profit < 0 // low-stock goods come up "red" profit immediately
                    || price < unitCost && isBuy
                    || price > unitCost && !isBuy
                    || profit < best)
                    break;
                best = profit;
                numToTrade = i;
                DDTweaks.Log.LogWarning($"{i} => Price: {price}, Profit: {profit}, Volume: {numToTrade}/{howManyGoodsToCheck}");
            }

            if (best > 0)
            {
                var percent = (float)numToTrade / howManyGoodsToCheck;
                DDTweaks.Log.LogError(percent);
                slider.SetValueWithoutNotify(percent);
            }
        }

        // slight vanilla rewrite avoids trying to get cityData when we don't even need it (and it throws, missing a null check)
        [HarmonyPatch(typeof(MarketManager), "GetPrice", typeof(int), typeof(int), typeof(int), typeof(bool))]
        public static void Prefix(ref bool __runOriginal, int id, int citycount, int playercount, bool IsBuy, MarketManager __instance, ref float __result)
        {
            __runOriginal = false;
            if (genericMethod == null) return;
            var flag1 = false;
            var flag2 = false;
            var num1 = 1f;
            if (cityMgr.TryGetCityModule<SpecialGoods>(id, out var cityModule1) && cityModule1.SpecialGoodsType.Contains(id))
                flag1 = true;
            if (cityMgr.TryGetCityModule<LackGoods>(id, out var cityModule2) && cityModule2.LackGoodsType.Contains(id))
                flag2 = true;
#pragma warning disable Harmony003
            var num2 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "LowValue");
            var num3 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "HighValue");
            var num4 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "HighPrice");
            var num5 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "LowPrice");
            var num6 = IsBuy ? 1.1f : 1f;
            if (flag1)
            {
                num2 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "LowMinValue");
                num3 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "HighValue");
                num4 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "HighPrice");
                num5 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "MinPrice");
                num1 = IsBuy ? 0.8f : 0.75f;
            }
            else if (flag2)
            {
                num2 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "LowValue");
                num3 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "HighMaxValue");
                num4 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "MaxPrice");
                num5 = MonoSingleton<Data>.Instance.TableAgent.GetFloat("Shop", id.ToString(), "LowPrice");
#pragma warning restore Harmony003
                num1 = IsBuy ? 1.25f : 1.2f;
            }

            __result = (citycount - playercount >= (double)num2
                ? citycount - playercount <= (double)num3
                    ? (float)((num4 - (double)num5) / (num2 - (double)num3) * (citycount - playercount - (double)num2)) + num4
                    : num5
                : num4) * num6 * num1;
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