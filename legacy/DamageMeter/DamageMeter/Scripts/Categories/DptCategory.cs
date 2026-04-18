using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DamageMeter.Scripts.Categories;

public class DptCategory : IStatCategory, ITrendChartCategory
{
	public string Name => I18n.CatDpt;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		int currentTurn = CombatDataCollector.SelectedViewTurnCount;
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.DamageDealt))
		{
			int value = ((currentTurn > 0) ? (item.DamageDealt / currentTurn) : 0);
			list.Add(new BarData
			{
				Key = item.Key,
				Label = item.Name,
				Value = value
			});
		}
		return list;
	}

	public List<BarData> GetDetailBars(string playerKey)
	{
		if (!CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return new List<BarData>();
		}
		List<BarData> list = new List<BarData>();
		foreach (KeyValuePair<int, int> item in value.DamagePerTurn.OrderBy((KeyValuePair<int, int> k) => k.Key))
		{
			list.Add(new BarData
			{
				Key = item.Key.ToString(),
				Label = $"{I18n.Turn} {item.Key}",
				Value = item.Value
			});
		}
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

	public TrendChartData? GetDashboardTrend(string? playerKey)
	{
		TrendChartData trendChartData = new TrendChartData
		{
			Title = I18n.Get((playerKey == null) ? "chart_team_damage_tempo" : "chart_player_damage_tempo", (playerKey == null) ? "Team Output Trend" : "Output Per Turn"),
			LineColor = new Color(1f, 0.72f, 0.3f, 1f)
		};
		int selectedViewTurnCount = CombatDataCollector.SelectedViewTurnCount;
		for (int i = 1; i <= selectedViewTurnCount; i++)
		{
			int num = 0;
			if (playerKey == null)
			{
				foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
				{
					if (value.DamagePerTurn.TryGetValue(i, out int value2))
					{
						num += value2;
					}
				}
			}
			else if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value3) && value3.DamagePerTurn.TryGetValue(i, out int value4))
			{
				num = value4;
			}
			trendChartData.Values.Add(num);
		}
		if (!trendChartData.Values.Any((int value) => value > 0))
		{
			return null;
		}
		int num2 = trendChartData.Values.Max();
		int num3 = trendChartData.Values.IndexOf(num2) + 1;
		trendChartData.Summary = $"{I18n.Get("hud_chart_peak", "Peak")} T{num3}: {num2}";
		return trendChartData;
	}
}
