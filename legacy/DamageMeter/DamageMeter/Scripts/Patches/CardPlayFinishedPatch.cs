using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "CardPlayFinished")]
public static class CardPlayFinishedPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, CardPlay cardPlay)
	{
		try
		{
			CombatDataCollector.RecordCardPlay(cardPlay);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"CardPlayFinishedPatch failed: {value}", 1);
		}
	}
}
