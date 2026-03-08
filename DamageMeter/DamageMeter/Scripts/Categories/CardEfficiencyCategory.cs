using System;
using System.Collections.Generic;

namespace DamageMeter.Scripts.Categories;

public class CardEfficiencyCategory : IStatCategory
{
	public string Name => I18n.CatEfficiency;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
		{
			if (value.TotalEnergySpent > 0)
			{
				float num = (float)value.DamageDealt / (float)value.TotalEnergySpent;
				int val = (int)(num * 10f);
				list.Add(new BarData
				{
					Key = value.Key,
					Label = value.Name,
					Value = Math.Max(1, val),
					DisplayText = $"{num:F1} DMG/E"
				});
			}
		}
		list.Sort((BarData a, BarData b) => b.Value.CompareTo(a.Value));
		return list;
	}

	public List<BarData> GetDetailBars(string playerKey)
	{
		if (!CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return new List<BarData>();
		}
		List<BarData> list = new List<BarData>();
		foreach (KeyValuePair<string, int> item in value.DamageByCard)
		{
			string key = item.Key;
			int value2 = item.Value;
			value.EnergySpentByCard.TryGetValue(key, out var value3);
			if (value3 > 0)
			{
				float num = (float)value2 / (float)value3;
				int val = (int)(num * 10f);
				list.Add(new BarData
				{
					Key = key,
					Label = CombatDataCollector.ResolveCardName(key),
					Value = Math.Max(1, val),
					DisplayText = $"{num:F1} ({value2}/{value3}E)"
				});
			}
		}
		list.Sort((BarData a, BarData b) => b.Value.CompareTo(a.Value));
		return list;
	}

	public string GetDetailTitle(string playerKey)
	{
		if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return value.Name;
		}
		return "";
	}
}
