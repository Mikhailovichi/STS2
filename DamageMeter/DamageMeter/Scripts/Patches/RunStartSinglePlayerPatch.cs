using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace DamageMeter.Scripts.Patches;

[HarmonyPatch(typeof(RunManager), "SetUpNewSinglePlayer")]
public static class RunStartSinglePlayerPatch
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
			MainFile.Log.Info("New single-player run detected - data reset", 1);
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"RunStartSinglePlayerPatch failed: {value}", 1);
		}
	}
}
