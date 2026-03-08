using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "CardDrawn")]
public static class CardDrawnPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, CardModel card, bool fromHandDraw)
	{
		try
		{
			CombatDataCollector.RecordCardDrawn(card);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"CardDrawnPatch failed: {value}", 1);
		}
	}
}
