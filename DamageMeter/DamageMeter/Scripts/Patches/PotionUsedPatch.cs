using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "PotionUsed")]
public static class PotionUsedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, PotionModel potion, Creature? target)
	{
		try
		{
			CombatDataCollector.RecordPotionUsed(potion, target);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"PotionUsedPatch failed: {value}", 1);
		}
	}
}
