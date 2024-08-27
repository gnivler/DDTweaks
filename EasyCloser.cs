using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using HarmonyLib;
using QxFramework.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DDTweaks;

public class EasyCloser : MonoBehaviour
{
    private readonly FieldInfo _openUI = AccessTools.Field(typeof(UIManager), "_openUI");
    private readonly Vector2 _screenCenter = new(Screen.width / 2, Screen.height / 2);
    private readonly FieldInfo _buttonList = AccessTools.Field(typeof(DialogWindowUI), "_buttonList");

    private bool IsIgnoredUI(UIBase ui) =>
        ui.GetComponentInChildren<Choose>() is not null
        // is a WareHouse that isn't at our location (eg caravan) or another caravan logic?  just avoid closing caravan trades
        || ui is NewEventUI or MainUI or ArchivementMainUI or HintUI or NewMapUI or LoadingUI or SCBattleWindow or BattleWindowNew
            or UnderAttackUI or WareHouseWindow or OverseaVersionStartUI or PlotUI;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            DoClose(closeAll: true);

        if (Input.GetMouseButtonDown(1))
            DoClose();
    }

    private void DoClose(bool closeAll = false)
    {
        if (closeAll)
        {
            var openUI = (List<KeyValuePair<string, UIBase>>)_openUI.GetValue(UIManager.Instance);
            for (var index = 0; index < openUI.Count; index++)
                Close();
        }
        else
            Close();

        void Close()
        {
            var ui = GetTopWindow();
            if (!ui || IsIgnoredUI(ui))
                return;
            // if the DialogWindowUI just has one button, click it, otherwise do nothing.  This ensures certain UIs still
            // appearing when chain-launched from another dialog having just a single button
            if (ui is DialogWindowUI)
            {
                var uiButtonList = (Transform)_buttonList.GetValue(ui);
                var activeButtons = uiButtonList.GetComponentsInChildren<Button>().Where(b => b.interactable).ToArray();
                if (activeButtons.Length == 1)
                {
                    DDTweaks.Log.LogWarning("<> Closing: " + ui.GetType().Name);
                    activeButtons[0].onClick.Invoke();
                }
            }
            else
            {
                DDTweaks.Log.LogWarning("<> Closing: " + ui.GetType().Name);
                UIManager.Instance.Close(ui);
            }
        }
    }

    private UIBase GetTopWindow()
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = _screenCenter }, results);
        var openUI = ((List<KeyValuePair<string, UIBase>>)_openUI.GetValue(UIManager.Instance)).Select(kvp => kvp.Value).ToList();
        foreach (var result in results.Where(x => x.gameObject))
        {
            var hitObject = result.gameObject;
            var uiBase = hitObject?.GetComponentInParent<UIBase>();
            if (!hitObject
                || !uiBase
                || !openUI.Contains(uiBase))
                continue;
            DDTweaks.Log.LogInfo($"GetTopWindow: {uiBase.GetType().Name}");
            return uiBase;
        }

        return null;
    }
}