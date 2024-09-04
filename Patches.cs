using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using App.Common;
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

        if (DDTweaks.modSettings.tradeDetails.Value)
            DDTweaks.harmony.PatchAll(typeof(TradeDetails));

        // if (DDTweaks.modSettings.peopleView.Value)
        //     DDTweaks.harmony.PatchAll(typeof(PeopleView));

        if (DDTweaks.modSettings.reroll.Value)
            DDTweaks.harmony.PatchAll(typeof(RerollSkills));

        if (File.Exists(@"C:\SteamLibrary\Dustland Delivery\BepInEx\plugins\gnivler"))
        {
            DDTweaks.gnivler = true;
            Log("gnivler mode");
            DDTweaks.harmony.PatchAll(typeof(gnivler));
        }
    }

    // private static class PeopleView
    // {
    //     // patch the MainUI to show the real name of the person instead of level
    //     [HarmonyPatch(typeof(MainUI), nameof(MainUI.Hero))]
    //     public static void Postfix(GameObject[] ___heros)
    //     {
    //         var people = MonoSingleton<Data>.Instance.Get<PeopleData>().NowPeople;
    //         for (var index = 0; index < people.Count; index++)
    //         {
    //             var hero = ___heros[index];
    //             var levelText = hero.transform.FindChildByName("LevelText_1").GetComponent<Text>();
    //             levelText.text = $"{people[index].RealName}\nL{people[index].Experience.level}";
    //             // levelText.gameObject.AddComponent<AutoFontSize>();
    //         }
    //     }
    //
    //     // patch the character screen UI to reverse the string positions
    // }

    private static class RerollSkills
    {
        private static bool consumeBooks = true;

        private static void ConfigureButton(RectTransform buttonRt, string text, string name = "DDTweaks.GameObject")
        {
            buttonRt.pivot = new Vector2(0.5f, 0.5f);
            buttonRt.sizeDelta = new Vector2(230f, buttonRt.sizeDelta.y);
            buttonRt.gameObject.name = name;
            buttonRt.GetComponentInChildren<Text>().text = text;
        }

        [HarmonyPatch(typeof(PeopleUpgradeManager), nameof(PeopleUpgradeManager.UsePointToGetBuff))]
        public static void Postfix(PeopleUpgradeManager __instance, Personal b, int Book)
        {
            try
            {
                // pillage and rebuild some UI
                var chooseBuffUI = Object.FindObjectOfType<ChooseBuffUI>();
                var cloneSource = Singleton<ResourceManager>.Instance.Instantiate(UIManager.Instance.FoldPath + nameof(ModUI));
                var modUI = cloneSource.GetComponent<ModUI>();
                var toggleButton = Object.Instantiate(modUI.Get<Button>("EnableBtn").gameObject, chooseBuffUI.transform).GetComponent<Button>();
                var rollButton = Object.Instantiate(modUI.Get<Button>("CancelBtn").gameObject, toggleButton.transform).GetComponent<Button>();
                var rollButtonRt = rollButton.GetComponentInChildren<RectTransform>();
                ConfigureButton(rollButtonRt, "Re-roll Choices", "DDTweaks.RollButton");
                var toggleButtonRt = toggleButton.transform.GetComponent<RectTransform>();
                ConfigureButton(toggleButtonRt, $"Toggle Consume {Book} Books", "DDTweaks.ConsumeBooksToggleButton");
                Object.Destroy(cloneSource);
                var toggleText = toggleButton.transform.Find("Label").GetComponent<Text>();
                toggleText.alignment = TextAnchor.MiddleRight;
                toggleText.resizeTextForBestFit = true;
                toggleText.alignByGeometry = true;
                toggleButtonRt.localPosition = new Vector3(0, 240, 0);
                rollButton.transform.localPosition = new Vector3(0, -toggleButtonRt.sizeDelta.y - 7f, 0);
                var checkmark = toggleButtonRt.transform.Find("Background/EnableCheckmark").GetComponent<Image>();
                checkmark.enabled = consumeBooks;
                ConfigureOnClick(__instance, b, Book, toggleButton, rollButton, checkmark, chooseBuffUI);
                if (consumeBooks && GameMgr.Get<IItemManager>().GetGoodsCout(1008) < Book * 2)
                    rollButton.GetComponent<Button>().interactable = false;
            }
            catch (Exception e)
            {
                Log(e);
            }
        }

        private static void ConfigureOnClick(PeopleUpgradeManager __instance, Personal b, int Book, Button toggleButton, Button rollButton, Image checkmark, ChooseBuffUI chooseBuffUI)
        {
            toggleButton.onClick.RemoveAllListeners();
            rollButton.onClick.RemoveAllListeners();
            toggleButton.onClick.AddListener(() =>
            {
                consumeBooks = !consumeBooks;
                checkmark.enabled = consumeBooks;
                if (!consumeBooks)
                    rollButton.GetComponent<Button>().interactable = true;
                else if (GameMgr.Get<IItemManager>().GetGoodsCout(1008) < Book * 2)
                    rollButton.GetComponent<Button>().interactable = false;
            });
            rollButton.onClick.AddListener(() =>
            {
                chooseBuffUI.DoClose();
                if (consumeBooks)
                {
                    GameMgr.Get<IItemManager>().AddGoods(1008, -Book);
                    var heroUpgradeWindow = (HeroUpgradeWindow)UIManager.Instance.GetUI("HeroUpgradeWindow");
                    heroUpgradeWindow.RefreshUI(heroUpgradeWindow.Person);
                }

                __instance.UsePointToGetBuff(b, Book);
                Object.Destroy(toggleButton.gameObject);
                Object.Destroy(rollButton.gameObject);
            });
        }
    }

    private static class gnivler
    {
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

    private static class TradeDetails
    {
        private const double MinutesInWeek = 7 * 24 * 60;

        [HarmonyPatch(typeof(TradePanel), "OnDisplay")]
        public static void Postfix(TradePanel __instance)
        {
            var cloneSource = __instance.transform.Find("FractionWindow/OutPutBG").gameObject;
            var windowFrameImage = cloneSource.transform.parent.GetComponent<Image>();
            var clone = Object.Instantiate(cloneSource, windowFrameImage.transform, true);
            clone.name = "DDTweaks.NeedsSummary";
            clone.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            var rt = clone.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(330, 800);

            var needsBody = clone.transform.Find("OutputDes").gameObject.GetComponent<Text>();
            var needsHeader = Object.Instantiate(needsBody, clone.transform);
            needsHeader.name = "ExistingRoutes";
            needsHeader.text = "<b><color=#FFD700>Existing Routes</color></b>";

            // Reposition needsHeader such that it is horizontally centered and flush with the top
            var headerRT = needsHeader.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0.5f, 1);
            headerRT.anchorMax = new Vector2(0.5f, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, -10);

            // Apply ContentSizeFitter to ensure text fits content
            var headerCSF = needsHeader.gameObject.AddComponent<ContentSizeFitter>();
            headerCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            headerCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            needsBody.name = "NeedsBody";
            needsBody.text = BuildNeedsString(__instance.selfCity, __instance.selfCity.TradeCityDatas.Where(d => d.Signed).ToList());
            needsBody.fontSize = Convert.ToInt32(needsBody.fontSize * 0.66f);
            var scaleFactor = 1f;
            if (windowFrameImage.canvas.GetComponent<CanvasScaler>() is { uiScaleMode: CanvasScaler.ScaleMode.ScaleWithScreenSize } canvasScaler)
            {
                var currentResolution = new Vector2(Screen.width, Screen.height);
                var widthScale = currentResolution.x / canvasScaler.referenceResolution.x;
                var heightScale = currentResolution.y / canvasScaler.referenceResolution.y;
                scaleFactor = Mathf.Lerp(widthScale, heightScale, canvasScaler.matchWidthOrHeight);
            }

            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.localPosition = new Vector3(rt.localPosition.x + rt.rect.width / 2 * scaleFactor + 20, 0, 0);
            // Log($"scaleFactor: {scaleFactor} localPosition ({rt.localPosition.x}, {rt.localPosition.y}, {rt.localPosition.z})");
        }

        private static string BuildNeedsString(SelfCity selfCity, List<TradeCityData> tradeCityData)
        {
            Dictionary<string, int[]> needsDictionary = new();
            for (var index = 0; index < tradeCityData.Count; ++index)
                foreach (var key in tradeCityData[index].NeedGoods.Keys)
                {
                    var needs = tradeCityData[index].NeedGoods[key];
                    var twentyPercent = Mathf.RoundToInt(Math.Max(1f, needs * 0.2f));
                    var min = Mathf.RoundToInt(Math.Max(0, needs - twentyPercent)); // this is just arbitrary, copied from game
                    var max = needs + twentyPercent;
                    if (max <= 1)
                        continue;
                    var goodsName = GetGoodsName(key);
                    if (needsDictionary.TryGetValue(goodsName, out var counts))
                        needsDictionary[goodsName] = [counts[0] + min, counts[1] + max];
                    else
                        needsDictionary.Add(goodsName, [min, max]);
                }

            StringBuilder result = new();
            var production = GetProductionNormalizedTo7Days(selfCity);
            foreach (var goodsName in needsDictionary.OrderBy(kvp => kvp.Key))
            {
                string color;
                production.TryGetValue(goodsName.Key, out var count);
                if (count < needsDictionary[goodsName.Key][0])
                    color = "red";
                else if (count < needsDictionary[goodsName.Key][1])
                    color = "yellow";
                else
                    color = "green";

                var produced = $"<color={color}>({count})</color>";
                result.AppendLine($"<b>{goodsName.Key}:</b> {needsDictionary[goodsName.Key][0]}~{needsDictionary[goodsName.Key][1]} {produced}");
            }

            return result.ToString();
        }

        private static string GetGoodsName(int id) =>
            MonoSingleton<Data>.Instance.TableAgent.GetString("Objects", MonoSingleton<Data>.Instance.TableAgent.GetString("Shop", id.ToString(), "Object_ID"), "Name");

        private static Dictionary<string, int> GetProductionNormalizedTo7Days(SelfCity selfCity)
        {
            Dictionary<string, StockInfo> stockInfos = new();
            var storeData = (GameMgr.Get<ISelfCityManager>().GetCityBySelf(selfCity).Modules.Find(m => m is GoodsStore) as GoodsStore)?.storeData;
            foreach (var cityJob1 in selfCity.CityJobs)
            foreach (var cityJob2 in cityJob1.Value.Where(v => v.craftId != -1))
                if (storeData > cityJob2.GetRawMaterial())
                {
                    var craftedOutputId = cityJob2.craftId.ToString();
                    var output = cityJob2.GetOutput();
                    // Get the production duration time in minutes from the game data
                    var productionTimeInMinutes = MonoSingleton<Data>.Instance.TableAgent.GetInt("SelfCityCraft", craftedOutputId, "Time");
                    // Log($"Able to produce {goods} (id {key1}) in {productionTimeInMinutes} minutes");

                    // Normalize the output to a 7-day period (7 days * 24 hours/day * 60 minutes/hour)
                    var multiplier = MinutesInWeek / productionTimeInMinutes;
                    AccumulateOutput(output, multiplier);
                }

            return CondenseResult();

            void AccumulateOutput(StockInfo output, double multiplier)
            {
                foreach (var item in output.items)
                {
                    item.GoodsCout = Convert.ToInt32(item.GoodsCout * multiplier);
                    if (stockInfos.ContainsKey(item.Name))
                        stockInfos[item.Name] += output;
                    else
                        stockInfos[item.Name] = output;
                }
            }

            Dictionary<string, int> CondenseResult()
            {
                Dictionary<string, int> result = new();
                foreach (var item in stockInfos.SelectMany(kvp => kvp.Value.items))
                    if (result.TryGetValue(item.Name, out _))
                        result[item.Name] += item.GoodsCout;
                    else
                        result.Add(item.Name, item.GoodsCout);

                return result;
            }
        }
    }

    private static class ProfitSliders
    {
        private static readonly MethodInfo method = typeof(GameMgr).GetMethod("Get", []);
        private static readonly Type iCity = AccessTools.TypeByName("ICityManager");
        private static readonly MethodInfo iCityManagerGet = method?.MakeGenericMethod(iCity);
        private static readonly CityManager cityMgr = (CityManager)iCityManagerGet.Invoke(null, null);

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
                    Log($"Break: profit < 0: {profit < 0} (adjPrice < unitCost && isBuy): {adjPrice < unitCost && isBuy} (adjPrice > unitCost && !isBuy): {adjPrice > unitCost && !isBuy}" +
                        $" profit < best: {profit < best} (unitCost {unitCost})");
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

    private static class Rarity
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

    private static class Books
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

    private static class Tires
    {
        // just reruns what was done in the method, to add back the wear
        public static void StateChangePostfix()
        {
            var wear = MonoSingleton<Data>.Instance.Get<AutoData>()._autoMain.Speed / 60f;
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
        }
    }

    private static class Junk
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
    }
}