using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace DamageMeter.Scripts;

public static class I18n
{
	[CompilerGenerated]
	private sealed class _003CGetLanguageCandidates_003Ed__112 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string language;

		public string _003C_003E3__language;

		private HashSet<string> _003Cseen_003E5__2;

		string IEnumerator<string>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CGetLanguageCandidates_003Ed__112(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = System.Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003Cseen_003E5__2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num;
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003Cseen_003E5__2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				if (_003Cseen_003E5__2.Add(language))
				{
					_003C_003E2__current = language;
					_003C_003E1__state = 1;
					return true;
				}
				goto IL_006d;
			case 1:
				_003C_003E1__state = -1;
				goto IL_006d;
			case 2:
				_003C_003E1__state = -1;
				goto IL_00b2;
			case 3:
				_003C_003E1__state = -1;
				goto IL_00f2;
			case 4:
				_003C_003E1__state = -1;
				goto IL_0132;
			case 5:
				{
					_003C_003E1__state = -1;
					break;
				}
				IL_0132:
				if (_003Cseen_003E5__2.Add("en"))
				{
					_003C_003E2__current = "en";
					_003C_003E1__state = 5;
					return true;
				}
				break;
				IL_006d:
				num = language.IndexOf('_');
				if (num > 0)
				{
					string item = language.Substring(0, num);
					if (_003Cseen_003E5__2.Add(item))
					{
						_003C_003E2__current = item;
						_003C_003E1__state = 2;
						return true;
					}
				}
				goto IL_00b2;
				IL_00f2:
				if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase) && _003Cseen_003E5__2.Add("en"))
				{
					_003C_003E2__current = "en";
					_003C_003E1__state = 4;
					return true;
				}
				goto IL_0132;
				IL_00b2:
				if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) && _003Cseen_003E5__2.Add("zhs"))
				{
					_003C_003E2__current = "zhs";
					_003C_003E1__state = 3;
					return true;
				}
				goto IL_00f2;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CGetLanguageCandidates_003Ed__112 _003CGetLanguageCandidates_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == System.Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CGetLanguageCandidates_003Ed__ = this;
			}
			else
			{
				_003CGetLanguageCandidates_003Ed__ = new _003CGetLanguageCandidates_003Ed__112(0);
			}
			_003CGetLanguageCandidates_003Ed__.language = _003C_003E3__language;
			return _003CGetLanguageCandidates_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private const string DefaultLanguage = "en";

	private const string ResourcePrefix = "DamageMeter.localization.";

	private static Dictionary<string, string> _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static string? _loadedLanguage;

	private static bool _subscribed;

	public static string CatDamageDealt => Get("cat_damage_dealt", "Total Output");

	public static string CatRealDamage => Get("cat_real_damage", "Actual Damage");

	public static string CatDamageTaken => Get("cat_damage_taken", "Damage Taken");

	public static string CatDpt => Get("cat_dpt", "Output / Turn");

	public static string CatAssistDamage => Get("cat_assist_damage", "Team Buff Damage");

	public static string CatAssistBlock => Get("cat_assist_block", "Team Block Support");

	public static string CatCardUsage => Get("cat_card_usage", "Cards Played");

	public static string CatBlock => Get("cat_block", "Block");

	public static string CatEnergy => Get("cat_energy", "Energy");

	public static string CatEfficiency => Get("cat_efficiency", "Damage per Energy");

	public static string CatOverkill => Get("cat_overkill", "Overkill");

	public static string CatPotions => Get("cat_potions", "Potion Count");

	public static string CatDebuffs => Get("cat_debuffs", "Debuffs Applied");

	public static string Turn => Get("turn", "Turn");

	public static string Wasted => Get("wasted", "wasted");

	public static string Spent => Get("spent", "spent");

	public static string TotalWasted => Get("total_wasted", "Total Wasted");

	public static string Blocked => Get("blocked", "Blocked");

	public static string WaitingForCombat => Get("waiting", "Waiting for combat...");

	public static string SegmentCurrent => Get("segment_current", "Current Fight");

	public static string SegmentOverall => Get("segment_overall", "Total");

	public static string SegmentFight => Get("segment_fight", "Fight");

	public static string CatDeathLog => Get("cat_death_log", "Death Record");

	public static string CatCardFlow => Get("cat_card_flow", "Draw/Discard/Exhaust");

	public static string DeathLog => Get("death_log", "Death Record");

	public static string Dead => Get("dead", "DEAD");

	public static string Alive => Get("alive", "Alive");

	public static string Drawn => Get("drawn", "Drawn");

	public static string Discarded => Get("discarded", "Discarded");

	public static string Exhausted => Get("exhausted", "Exhausted");

	public static string CatCombatLog => Get("cat_combat_log", "Combat Record");

	public static string CatRecords => Get("cat_records", "Lifetime Records");

	public static string Events => Get("events", "events");

	public static string RecHighestHit => Get("rec_highest_hit", "Highest Output Hit");

	public static string RecMostFightDmg => Get("rec_most_fight_dmg", "Most Fight Output");

	public static string RecBestTurnDmg => Get("rec_best_turn_dmg", "Best Turn Output");

	public static string RecMostCards => Get("rec_most_cards", "Most Cards Played");

	public static string RecMostBlock => Get("rec_most_block", "Most Block Gained in a Fight");

	public static string RecTotalDamage => Get("rec_total_damage", "Total Output");

	public static string RecTotalFights => Get("rec_total_fights", "Fights Completed");

	public static string RecNoRecords => Get("rec_no_records", "No records yet");

	public static string Settings => Get("settings", "Settings");

	public static string SettingsScale => Get("settings_scale", "Scale");

	public static string SettingsOpacity => Get("settings_opacity", "HUD Opacity");

	public static string SettingsMaxBars => Get("settings_max_bars", "Max Rows");

	public static string SettingsResetPos => Get("settings_reset_pos", "Reset HUD Position");

	public static string ResetData => Get("reset_data", "Clear Stats");

	public static string SettingsAutoReset => Get("settings_auto_reset", "Auto-clear on new run");

	public static string Dashboard => Get("dashboard", "Overview Panel");

	public static event Action? Changed;

	public static string Get(string key, string fallback)
	{
		EnsureLoaded();
		return _translations.GetValueOrDefault(key) ?? fallback;
	}

	public static void Initialize()
	{
		ForceReload();
		TrySubscribe();
	}

	private static void TrySubscribe()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		if (_subscribed)
		{
			return;
		}
		try
		{
			LocManager instance = LocManager.Instance;
			if (instance != null)
			{
				instance.SubscribeToLocaleChange(OnLocaleChanged);
				_subscribed = true;
				MainFile.Log.Info("Subscribed to LocManager locale change", 1);
			}
			else
			{
				MainFile.Log.Info("LocManager.Instance is null, will use lazy language detection", 1);
			}
		}
		catch (Exception ex)
		{
			MainFile.Log.Error("Failed to subscribe to locale change: " + ex.Message, 1);
		}
	}

	private static void OnLocaleChanged()
	{
		string text = ResolveLanguage();
		MainFile.Log.Info("Locale changed callback fired, resolved language: " + text, 1);
		_loadedLanguage = null;
		ForceReload();
		I18n.Changed?.Invoke();
	}

	private static void EnsureLoaded()
	{
		if (!_subscribed)
		{
			TrySubscribe();
		}
		string text = ResolveLanguage();
		if (!string.Equals(_loadedLanguage, text, StringComparison.OrdinalIgnoreCase))
		{
			_translations = LoadTranslations(text);
			_loadedLanguage = text;
			MainFile.Log.Info($"Lazy-loaded localization: {text} ({_translations.Count} keys)", 1);
		}
	}

	private static void ForceReload()
	{
		string text = ResolveLanguage();
		_translations = LoadTranslations(text);
		_loadedLanguage = text;
		MainFile.Log.Info($"Localization loaded: {text} ({_translations.Count} keys)", 1);
	}

	private static Dictionary<string, string> LoadTranslations(string language)
	{
		foreach (string languageCandidate in GetLanguageCandidates(language))
		{
			Dictionary<string, string> dictionary = TryLoadEmbedded(languageCandidate);
			if (dictionary != null && dictionary.Count > 0)
			{
				return dictionary;
			}
			dictionary = TryLoadFromPck("res://DamageMeter/localization/" + languageCandidate + ".json");
			if (dictionary != null && dictionary.Count > 0)
			{
				return dictionary;
			}
		}
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}

	private static Dictionary<string, string>? TryLoadEmbedded(string language)
	{
		string text = "DamageMeter.localization." + language + ".json";
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(text);
		if (stream == null)
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
		}
		catch (JsonException ex)
		{
			MainFile.Log.Error("Failed to parse embedded localization '" + text + "': " + ex.Message, 1);
			return null;
		}
	}

	private static Dictionary<string, string>? TryLoadFromPck(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
		{
			return null;
		}
		Godot.FileAccess val = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		try
		{
			if (val == null)
			{
				return null;
			}
			try
			{
				return JsonSerializer.Deserialize<Dictionary<string, string>>(val.GetAsText(false));
			}
			catch (JsonException ex)
			{
				MainFile.Log.Error("Failed to parse localization file '" + path + "': " + ex.Message, 1);
				return null;
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static string ResolveLanguage()
	{
		string text = null;
		try
		{
			LocManager instance = LocManager.Instance;
			text = ((instance != null) ? instance.Language : null);
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			try
			{
				text = TranslationServer.GetLocale();
			}
			catch
			{
			}
		}
		return NormalizeLanguageCode(text);
	}

	[IteratorStateMachine(typeof(_003CGetLanguageCandidates_003Ed__112))]
	private static IEnumerable<string> GetLanguageCandidates(string language)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CGetLanguageCandidates_003Ed__112(-2)
		{
			_003C_003E3__language = language
		};
	}

	private static string NormalizeLanguageCode(string? language)
	{
		if (string.IsNullOrWhiteSpace(language))
		{
			return "en";
		}
		string text = language.Trim().Replace('-', '_').ToLowerInvariant();
		switch (text)
		{
		case "zh_cn":
		case "zh_hans":
		case "zh_sg":
		case "zh":
			return "zhs";
		case "en_us":
		case "en_gb":
			return "en";
		default:
			return text;
		}
	}
}
