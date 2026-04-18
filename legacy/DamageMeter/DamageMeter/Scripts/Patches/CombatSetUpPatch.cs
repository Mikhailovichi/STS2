using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatManager), "SetUpCombat")]
public static class CombatSetUpPatch
{
	[HarmonyPostfix]
	public static void Postfix(CombatState state)
	{
		try
		{
			string encounterKey = "";
			try
			{
				EncounterModel encounter = state.Encounter;
				encounterKey = ((encounter != null) ? ((AbstractModel)encounter).Id.Entry : null) ?? "";
			}
			catch
			{
			}
			CombatDataCollector.ArchiveAndStartNew(encounterKey);
			CombatManager.Instance.TurnStarted -= CombatDataCollector.OnTurnStarted;
			CombatManager.Instance.TurnEnded -= CombatDataCollector.OnTurnEnded;
			CombatManager.Instance.TurnStarted += CombatDataCollector.OnTurnStarted;
			CombatManager.Instance.TurnEnded += CombatDataCollector.OnTurnEnded;
			DamageMeterUI.CreateAndAttach();
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"CombatSetUpPatch failed: {value}", 1);
		}
	}
}
