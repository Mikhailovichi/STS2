using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "CardDiscarded")]
public static class CardDiscardedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, CardModel card)
	{
		try
		{
			CombatDataCollector.RecordCardDiscarded(card);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"CardDiscardedPatch failed: {value}", 1);
		}
	}
}
