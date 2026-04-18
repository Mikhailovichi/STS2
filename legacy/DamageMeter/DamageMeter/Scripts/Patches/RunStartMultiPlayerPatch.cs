using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(RunManager), "SetUpNewMultiPlayer")]
public static class RunStartMultiPlayerPatch
{
	[HarmonyPostfix]
	public static void Postfix()
	{
		try
		{
			if (!DamageMeterSettings.AutoResetOnNewRun)
			{
				return;
			}

			CombatDataCollector.FinalizeCurrentCombatForRunReset();
			CombatDataCollector.ResetAll();
			MainFile.Log.Info("New multi-player run detected - data reset", 1);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"RunStartMultiPlayerPatch failed: {value}", 1);
		}
	}
}
