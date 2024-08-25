﻿using System.Linq;
using App.Common;
using HarmonyLib;
using QxFramework.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DDTweaks;

public class QuickCombat : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return)
            && GameMgr.Get<IBattleManager>() is { } mgr
            && mgr.GetCurrentBattle() is { CurDistance: > 1 } curBattle)
        {
            var lowestDefense = curBattle.CurBattlePersonal.Values.Min(p => p.Defend);
            var enemyDamage = (int)((string.IsNullOrEmpty(curBattle.SelfCity) ?
                MonoSingleton<Data>.Instance.Get<AutoData>()._autoMain.FightValue 
                : MonoSingleton<Data>.Instance.Get<CurrentSelfCityData>().SelfCities[curBattle.SelfCity].GetCityBattle()) * (1f - curBattle.EnemyPersonal.battleSpaecialStatus.DownDamage / 100f));
            enemyDamage = Mathf.Clamp((int)(curBattle.EnemyBattle * (1f + curBattle.EnemyPersonal.battleSpaecialStatus.Fang / 100f)) - enemyDamage, 0, int.MaxValue);
            var battleWindowNew = FindObjectOfType<BattleWindowNew>();
            // FileLog.Log(enemyDamage.ToString());
            if (enemyDamage < lowestDefense)
            {
                // FileLog.Log($"<> Coup de gras: {curBattle.EnemyBattle} < {lowestDefense}");
                mgr.PushForward(false, int.MaxValue);
                battleWindowNew.Fight();
            }
            else if (mgr.AllShoot())
            {
                battleWindowNew = FindObjectOfType<BattleWindowNew>();
                var shootButton = battleWindowNew._gos["BattleBtnShoot"];
                var nextButton = battleWindowNew._gos["BattleBtnNext"];
                ClickButton(shootButton);
                System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => ClickButton(nextButton));
            }
        }
    }

    private void ClickButton(GameObject button)
    {
        var pointerClickEvent = new PointerEventData(EventSystem.current);
        pointerClickEvent.position = Vector2.zero;
        ExecuteEvents.Execute(button, pointerClickEvent, ExecuteEvents.pointerClickHandler);
    }
}