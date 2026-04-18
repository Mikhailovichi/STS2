using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "CardExhausted")]
public static class CardExhaustedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, CardModel card)
	{
		try
		{
			CombatDataCollector.RecordCardExhausted(card);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"CardExhaustedPatch failed: {value}", 1);
		}
	}
}
