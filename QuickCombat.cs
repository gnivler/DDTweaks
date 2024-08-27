using System;
using System.Linq;
using HarmonyLib;
using QxFramework.Core;
using UnityEngine;
using UnityEngine.UI;

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
            var enemyDamage = Mathf.Clamp((int)(curBattle.EnemyBattle * (1f + curBattle.EnemyPersonal.battleSpaecialStatus.Fang / 100f)), 0, int.MaxValue);
            var battleWindowNew = FindObjectOfType<BattleWindowNew>();
            var scBattleWindow = FindObjectOfType<SCBattleWindow>();
            var curWindow = default(UIBase);
            if (battleWindowNew is not null)
                curWindow = battleWindowNew;
            else if (scBattleWindow is not null)
                curWindow = scBattleWindow;
            if (curWindow is null)
                return;

            if (enemyDamage < lowestDefense && curBattle.EnemyPersonal.battleSpaecialStatus.Boom == 0)
            {
                DDTweaks.Log.LogWarning($"<> Coup de gras: {curBattle.EnemyBattle} < {lowestDefense}");
                mgr.PushForward(false, int.MaxValue);
                AccessTools.Method(curWindow.GetType(), "Fight").Invoke(curWindow, []);
            }
            else if (mgr.AllShoot())
            {
                var shootButton = curWindow._gos["BattleBtnShoot"];
                var nextButton = curWindow._gos["BattleBtnNext"];
                var defendButton = curWindow._gos["BattleBtnDefend"];
                var secondButton = defendButton ?? nextButton;
                shootButton.GetComponent<Button>().onClick.Invoke();
                System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => secondButton.GetComponent<Button>().onClick.Invoke());
            }
        }
    }
}