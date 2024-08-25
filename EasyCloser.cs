using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using QxFramework.Core;
using UnityEngine;

namespace DDTweaks;

public class EasyCloser : MonoBehaviour
{
    private readonly FieldInfo _openUI = AccessTools.Field(typeof(UIManager), "_openUI");
    private readonly List<string> _notToHook = ["Start_UI", "Main_UI", "CommandUI", "ArchivementMainUI", "AchievementMainUI"];

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
        if (!closeAll && list.Count > 1)
        {
            var anyUiWillDo = FindObjectOfType<UIBase>();
            UIManager.Instance.Close(GetTopWindow(anyUiWillDo));
        }
        else if (closeAll)
        {
            for (var index = 0; index < list.Count; index++)
            {
                var kvp = list[index];
                if (!_notToHook.Any(s => kvp.Value.name.StartsWith(s)))
                    UIManager.Instance.Close(kvp.Value);
            }
        }
    }

    private UIBase GetTopWindow(UIBase window)
    {
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