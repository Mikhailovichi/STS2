using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(CombatManager), "Reset")]
public static class CombatResetPatch
{
	[HarmonyPrefix]
	public static void Prefix()
	{
		try
		{
			CombatManager.Instance.TurnStarted -= CombatDataCollector.OnTurnStarted;
			CombatManager.Instance.TurnEnded -= CombatDataCollector.OnTurnEnded;
			CombatDataCollector.StopTracking();
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"CombatResetPatch failed: {value}", 1);
		}
	}
}
