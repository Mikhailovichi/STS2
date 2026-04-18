using System.Reflection;
using DamageMeter.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace DamageMeter;

[ModInitializer("Initialize")]
public class MainFile
{
	internal const string ModId = "DamageMeter";

	internal static readonly Logger Log = new Logger(ModId, (LogType)0);

	public static void Initialize()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
		DamageMeterSettings.Load();
		I18n.Initialize();
		DamageMeterUI.Initialize();
		Log.Info("DamageMeter initialized.", 1);
	}
}
