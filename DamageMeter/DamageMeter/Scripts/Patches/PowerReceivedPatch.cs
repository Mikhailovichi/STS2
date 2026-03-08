using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "PowerReceived")]
public static class PowerReceivedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, PowerModel power, decimal amount, Creature? applier)
	{
		try
		{
			CombatDataCollector.RecordPowerReceived(power, amount, applier);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"PowerReceivedPatch failed: {value}", 1);
		}
	}
}
