using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DamageMeter.Scripts.Categories;

public class EnergyCategory : IStatCategory, ITrendChartCategory
{
	public string Name => I18n.CatEnergy;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.TotalEnergySpent))
		{
			if (item.TotalEnergySpent <= 0)
			{
				continue;
			}

			list.Add(new BarData
			{
				Key = item.Key,
				Label = item.Name,
				Value = item.TotalEnergySpent,
				DisplayText = $"{item.TotalEnergySpent} {I18n.Spent}, {item.TotalEnergyWasted} {I18n.Wasted}"
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
		foreach (KeyValuePair<string, int> item in value.EnergySpentByCard.OrderByDescending((KeyValuePair<string, int> k) => k.Value))
		{
			list.Add(new BarData
			{
				Key = "spent_" + item.Key,
				Label = CombatDataCollector.ResolveCardName(item.Key),
				Value = item.Value,
				DisplayText = $"{item.Value} {I18n.Spent}"
			});
		}

		if (value.TotalEnergyWasted > 0)
		{
			list.Add(new BarData
			{
				Key = "total_wasted",
				Label = I18n.TotalWasted,
				Value = value.TotalEnergyWasted,
				DisplayText = $"{value.TotalEnergyWasted} {I18n.Wasted}"
			});

			int currentTurn = CombatDataCollector.SelectedViewTurnCount;
			for (int i = 1; i <= currentTurn; i++)
			{
				value.EnergyWastedPerTurn.TryGetValue(i, out int wastedEnergy);
				if (wastedEnergy <= 0)
				{
					continue;
				}

				list.Add(new BarData
				{
					Key = $"turn_{i}",
					Label = $"{I18n.Turn} {i}",
					Value = wastedEnergy,
					DisplayText = $"{wastedEnergy} {I18n.Wasted}"
				});
			}
		}

		return list;
	}

	public string GetDetailTitle(string playerKey)
	{
		if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return $"{value.Name} - {value.TotalEnergySpent} {I18n.Spent}, {value.TotalEnergyWasted} {I18n.Wasted}";
		}
		return "";
	}

	public TrendChartData? GetDashboardTrend(string? playerKey)
	{
		TrendChartData trendChartData = new TrendChartData
		{
			Title = I18n.Get("chart_wasted_energy_tempo", "Wasted Energy Tempo"),
			LineColor = new Color(0.45f, 0.82f, 1f, 1f)
		};
		int selectedViewTurnCount = CombatDataCollector.SelectedViewTurnCount;
		for (int i = 1; i <= selectedViewTurnCount; i++)
		{
			int num = 0;
			if (playerKey == null)
			{
				foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
				{
					if (value.EnergyWastedPerTurn.TryGetValue(i, out int value2))
					{
						num += value2;
					}
				}
			}
			else if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value3) && value3.EnergyWastedPerTurn.TryGetValue(i, out int value4))
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
