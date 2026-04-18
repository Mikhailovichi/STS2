using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "DamageReceived")]
public static class DamageReceivedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, Creature receiver, Creature? dealer, DamageResult result, CardModel? cardSource)
	{
		try
		{
			CombatDataCollector.RecordDamage(dealer, receiver, result, cardSource);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"DamageReceivedPatch failed: {value}", 1);
		}
	}
}
