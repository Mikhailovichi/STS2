using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "BlockGained")]
public static class BlockGainedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, Creature receiver, int amount, ValueProp props, CardPlay? cardPlay)
	{
		try
		{
			CombatDataCollector.RecordBlockGained(receiver, amount, cardPlay);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"BlockGainedPatch failed: {value}", 1);
		}
	}
}
