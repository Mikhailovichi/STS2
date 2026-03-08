using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DamageMeter.Scripts.Categories;

public class RealDamageCategory : IStatCategory, ITrendChartCategory
{
	public string Name => I18n.CatRealDamage;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		int currentTurn = CombatDataCollector.SelectedViewTurnCount;
		int totalRealDamage = CombatDataCollector.Players.Values.Sum((CombatDataCollector.PlayerStats p) => p.RealDamageDealt);
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.RealDamageDealt))
		{
			int perTurn = ((currentTurn > 0) ? (item.RealDamageDealt / currentTurn) : 0);
			float share = ((totalRealDamage > 0) ? ((float)item.RealDamageDealt / (float)totalRealDamage * 100f) : 0f);
			list.Add(new BarData
			{
				Key = item.Key,
				Label = item.Name,
				Value = item.RealDamageDealt,
				DisplayText = $"{perTurn}/t  ({item.RealDamageDealt}, {share:F1}%)  "
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
		foreach (KeyValuePair<string, int> item in value.RealDamageByCard.OrderByDescending((KeyValuePair<string, int> k) => k.Value))
		{
			list.Add(new BarData
			{
				Key = item.Key,
				Label = CombatDataCollector.ResolveCardName(item.Key),
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
			Title = I18n.Get((playerKey == null) ? "chart_team_real_damage_tempo" : "chart_player_real_damage_tempo", (playerKey == null) ? "Team Actual Damage Trend" : "Actual Damage Per Turn"),
			LineColor = new Color(0.35f, 0.82f, 1f, 1f)
		};
		int selectedViewTurnCount = CombatDataCollector.SelectedViewTurnCount;
		for (int i = 1; i <= selectedViewTurnCount; i++)
		{
			int num = 0;
			if (playerKey == null)
			{
				foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
				{
					if (value.RealDamagePerTurn.TryGetValue(i, out int value2))
					{
						num += value2;
					}
				}
			}
			else if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value3) && value3.RealDamagePerTurn.TryGetValue(i, out int value4))
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
