using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace DamageMeter.Scripts;

public static class DamageMeterSettings
{
	public class PersonalRecords
	{
		public int HighestHit { get; set; }

		public string HighestHitCard { get; set; } = "";


		public int MostFightDamage { get; set; }

		public int BestTurnDamage { get; set; }

		public int MostCardsPlayed { get; set; }

		public int MostBlockGained { get; set; }

		public long TotalDamage { get; set; }

		public int TotalFights { get; set; }
	}

	private const string SettingsPath = "user://DamageMeter_settings.json";

	public static float PanelX { get; set; } = float.NaN;


	public static float PanelY { get; set; } = float.NaN;


	public static float Scale { get; set; } = 1f;


	public static float Opacity { get; set; } = 0.88f;


	public static int MaxBars { get; set; } = 10;


	public static bool AutoResetOnNewRun { get; set; } = true;


	public static PersonalRecords Records { get; } = new PersonalRecords();


	public static bool HasSavedPosition
	{
		get
		{
			if (!float.IsNaN(PanelX))
			{
				return !float.IsNaN(PanelY);
			}
			return false;
		}
	}

	public static void UpdateRecords(IEnumerable<CombatDataCollector.PlayerStats> allPlayers)
	{
		int num = 0;
		foreach (CombatDataCollector.PlayerStats allPlayer in allPlayers)
		{
			num += allPlayer.DamageDealt;
			if (allPlayer.MaxSingleHit > Records.HighestHit)
			{
				Records.HighestHit = allPlayer.MaxSingleHit;
				Records.HighestHitCard = allPlayer.MaxSingleHitCard;
			}
			int num2 = allPlayer.DamagePerTurn.Values.DefaultIfEmpty(0).Max();
			if (num2 > Records.BestTurnDamage)
			{
				Records.BestTurnDamage = num2;
			}
			if (allPlayer.CardsPlayed > Records.MostCardsPlayed)
			{
				Records.MostCardsPlayed = allPlayer.CardsPlayed;
			}
			if (allPlayer.TotalBlockGained > Records.MostBlockGained)
			{
				Records.MostBlockGained = allPlayer.TotalBlockGained;
			}
		}
		if (num > Records.MostFightDamage)
		{
			Records.MostFightDamage = num;
		}
		Records.TotalDamage += num;
		Records.TotalFights++;
		Save();
	}

	public static void Load()
	{
		try
		{
			if (!Godot.FileAccess.FileExists("user://DamageMeter_settings.json"))
			{
				return;
			}
			Godot.FileAccess val = Godot.FileAccess.Open("user://DamageMeter_settings.json", Godot.FileAccess.ModeFlags.Read);
			try
			{
				if (val == null)
				{
					return;
				}
				JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(val.GetAsText(false));
				if (jsonElement.ValueKind != JsonValueKind.Object)
				{
					return;
				}
				if (jsonElement.TryGetProperty("panel_x", out var value) && value.ValueKind == JsonValueKind.Number)
				{
					PanelX = value.GetSingle();
				}
				if (jsonElement.TryGetProperty("panel_y", out var value2) && value2.ValueKind == JsonValueKind.Number)
				{
					PanelY = value2.GetSingle();
				}
				if (jsonElement.TryGetProperty("scale", out var value3) && value3.ValueKind == JsonValueKind.Number)
				{
					Scale = Math.Clamp(value3.GetSingle(), 0.5f, 2f);
				}
				if (jsonElement.TryGetProperty("opacity", out var value4) && value4.ValueKind == JsonValueKind.Number)
				{
					Opacity = Math.Clamp(value4.GetSingle(), 0.3f, 1f);
				}
				if (jsonElement.TryGetProperty("max_bars", out var value5) && value5.ValueKind == JsonValueKind.Number)
				{
					MaxBars = Math.Clamp(value5.GetInt32(), 3, 30);
				}
				if (jsonElement.TryGetProperty("auto_reset", out var value6))
				{
					AutoResetOnNewRun = value6.ValueKind != JsonValueKind.False;
				}
				if (jsonElement.TryGetProperty("records", out var value7) && value7.ValueKind == JsonValueKind.Object)
				{
					if (value7.TryGetProperty("highest_hit", out var value8) && value8.ValueKind == JsonValueKind.Number)
					{
						Records.HighestHit = value8.GetInt32();
					}
					if (value7.TryGetProperty("highest_hit_card", out var value9) && value9.ValueKind == JsonValueKind.String)
					{
						Records.HighestHitCard = value9.GetString() ?? "";
					}
					if (value7.TryGetProperty("most_fight_damage", out var value10) && value10.ValueKind == JsonValueKind.Number)
					{
						Records.MostFightDamage = value10.GetInt32();
					}
					if (value7.TryGetProperty("best_turn_damage", out var value11) && value11.ValueKind == JsonValueKind.Number)
					{
						Records.BestTurnDamage = value11.GetInt32();
					}
					if (value7.TryGetProperty("most_cards_played", out var value12) && value12.ValueKind == JsonValueKind.Number)
					{
						Records.MostCardsPlayed = value12.GetInt32();
					}
					if (value7.TryGetProperty("most_block_gained", out var value13) && value13.ValueKind == JsonValueKind.Number)
					{
						Records.MostBlockGained = value13.GetInt32();
					}
					if (value7.TryGetProperty("total_damage", out var value14) && value14.ValueKind == JsonValueKind.Number)
					{
						Records.TotalDamage = value14.GetInt64();
					}
					if (value7.TryGetProperty("total_fights", out var value15) && value15.ValueKind == JsonValueKind.Number)
					{
						Records.TotalFights = value15.GetInt32();
					}
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			MainFile.Log.Error("Failed to load settings: " + ex.Message, 1);
		}
	}

	public static void Save()
	{
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			if (!float.IsNaN(PanelX))
			{
				dictionary["panel_x"] = PanelX;
			}
			if (!float.IsNaN(PanelY))
			{
				dictionary["panel_y"] = PanelY;
			}
			dictionary["scale"] = Scale;
			dictionary["opacity"] = Opacity;
			dictionary["max_bars"] = MaxBars;
			dictionary["auto_reset"] = AutoResetOnNewRun;
			Dictionary<string, object> value = new Dictionary<string, object>
			{
				["highest_hit"] = Records.HighestHit,
				["highest_hit_card"] = Records.HighestHitCard,
				["most_fight_damage"] = Records.MostFightDamage,
				["best_turn_damage"] = Records.BestTurnDamage,
				["most_cards_played"] = Records.MostCardsPlayed,
				["most_block_gained"] = Records.MostBlockGained,
				["total_damage"] = Records.TotalDamage,
				["total_fights"] = Records.TotalFights
			};
			dictionary["records"] = value;
			Godot.FileAccess val = Godot.FileAccess.Open("user://DamageMeter_settings.json", Godot.FileAccess.ModeFlags.Write);
			try
			{
				if (val == null)
				{
					MainFile.Log.Error($"Failed to open settings for writing: {Godot.FileAccess.GetOpenError()}", 1);
				}
				else
				{
					val.StoreString(JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
					{
						WriteIndented = true
					}));
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			MainFile.Log.Error("Failed to save settings: " + ex.Message, 1);
		}
	}
}
