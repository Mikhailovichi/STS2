using System;
using System.Runtime.InteropServices;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;

namespace GodotPlugins.Game;

internal static class Main
{
	[UnmanagedCallersOnly(EntryPoint = "godotsharp_game_main_init")]
	private static godot_bool InitializeFromGameProject(nint godotDllHandle, nint outManagedCallbacks, nint unmanagedCallbacks, int unmanagedCallbacksSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			DllImportResolver resolver = new GodotDllImportResolver((IntPtr)godotDllHandle).OnResolveDllImport;
			NativeLibrary.SetDllImportResolver(typeof(GodotObject).Assembly, resolver);
			NativeFuncs.Initialize((IntPtr)unmanagedCallbacks, unmanagedCallbacksSize);
			ManagedCallbacks.Create((IntPtr)outManagedCallbacks);
			ScriptManagerBridge.LookupScriptsInAssembly(typeof(Main).Assembly);
			return (godot_bool)1;
		}
		catch (Exception value)
		{
			Console.Error.WriteLine(value);
			return GodotBoolExtensions.ToGodotBool(false);
		}
	}
}
