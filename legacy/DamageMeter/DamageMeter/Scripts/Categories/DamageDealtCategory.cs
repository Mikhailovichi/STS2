using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class DamageDealtCategory : IStatCategory
{
	public string Name => I18n.CatDamageDealt;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		int currentTurn = CombatDataCollector.SelectedViewTurnCount;
		int num = CombatDataCollector.Players.Values.Sum((CombatDataCollector.PlayerStats p) => p.DamageDealt);
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.DamageDealt))
		{
			int value = ((currentTurn > 0) ? (item.DamageDealt / currentTurn) : 0);
			float value2 = ((num > 0) ? ((float)item.DamageDealt / (float)num * 100f) : 0f);
			list.Add(new BarData
			{
				Key = item.Key,
				Label = item.Name,
				Value = item.DamageDealt,
				DisplayText = $"{value}/t  ({item.DamageDealt}, {value2:F1}%)  "
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
		foreach (KeyValuePair<string, int> item in value.DamageByCard.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> k) => k.Value))
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
}
