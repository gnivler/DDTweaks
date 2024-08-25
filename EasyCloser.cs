using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using HarmonyLib;
using QxFramework.Core;
using UnityEngine;

namespace DDTweaks;

public class EasyCloser : MonoBehaviour
{
    private readonly FieldInfo _openUI = AccessTools.Field(typeof(UIManager), "_openUI");
    private readonly List<string> _notToHook = ["Start_UI", "Main_UI", "CommandUI", "ArchivementMainUI", "AchievementMainUI"];
    

    private bool IsIgnoredUI(UIBase ui) =>
        ui is NewEventUI or MainUI or ArchivementMainUI or HintUI or NewMapUI or LoadingUI
            or UnderAttackUI or WareHouseWindow or DialogWindowUI;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            DoClose(closeAll: true);

        if (Input.GetMouseButtonDown(1))
            DoClose();
    }

    private void DoClose(bool closeAll = false)
    {
        var list = (List<KeyValuePair<string, UIBase>>)_openUI.GetValue(UIManager.Instance);
        if (list.Count <= 1)
            return;
        if (closeAll)
        {
            for (var index = 0; index < list.Count; index++)
            {
                var kvp = list[index];
                if (kvp.Value == null
                    || IsIgnoredUI(kvp.Value))
                    continue;
                if (!_notToHook.Any(s => kvp.Value.name.StartsWith(s)))
                {
                    // FileLog.Log("<> Closing: " + kvp.Value.ToString());
                    UIManager.Instance.Close(kvp.Value);
                }
            }
        }
        else
        {
            var anyUiWillDo = FindObjectOfType<UIBase>();
            var topWindow = GetTopWindow(anyUiWillDo);
            if (!_notToHook.Any(s => topWindow.name.StartsWith(s)) && !IsIgnoredUI(topWindow))
                UIManager.Instance.Close(topWindow);
        }
    }


    private UIBase GetTopWindow(UIBase window)
    {
        if (IsIgnoredUI(window))
            return null;

        if (window.transform.parent is null)
            return window;

        var parent = window.transform.parent;
        var result = default(UIBase);
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.GetSiblingIndex() == parent.childCount - 1)
            {
                result = child.GetComponentInParent<UIBase>();
                break;
            }
        }

        return result;
    }
}