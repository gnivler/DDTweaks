using System.Collections;
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
            var curWindow = GetCurWindow();
            if (enemyDamage < lowestDefense && curBattle.EnemyPersonal.battleSpaecialStatus.Boom == 0)
            {
                DDTweaks.Log.LogWarning($"<> Coup de gras: {curBattle.EnemyBattle} < {lowestDefense}");
                mgr.PushForward(false, int.MaxValue);
                AccessTools.Method(curWindow.GetType(), "Fight").Invoke(curWindow, []);
            }
            else if (mgr.AllShoot())
            {
                StartCoroutine(SimulateButtonClicks());
            }
        }
    }

    private IEnumerator SimulateButtonClicks()
    {
        var curWindow = GetCurWindow();
        if (curWindow is null)
            yield break;

        var shootButton = curWindow._gos["BattleBtnShoot"];
        var nextButton = curWindow._gos["BattleBtnNext"];
        var defendButton = curWindow._gos["BattleBtnDefend"];
        var secondButton = defendButton.GetComponent<Image>().enabled ? defendButton : nextButton;
        shootButton.GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSeconds(0.35f);
        // Choose the appropriate second button
        secondButton.GetComponent<Button>().onClick.Invoke();
    }

    private static UIBase GetCurWindow()
    {
        var battleWindowNew = FindObjectOfType<BattleWindowNew>();
        var scBattleWindow = FindObjectOfType<SCBattleWindow>();
        if (battleWindowNew is not null)
            return battleWindowNew;
        return scBattleWindow;
    }
}