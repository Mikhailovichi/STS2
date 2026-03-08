using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class PotionCategory : IStatCategory
{
	public string Name => I18n.CatPotions;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.PotionsUsed))
		{
			if (item.PotionsUsed > 0)
			{
				list.Add(new BarData
				{
					Key = item.Key,
					Label = item.Name,
					Value = item.PotionsUsed
				});
			}
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
		foreach (KeyValuePair<string, int> item in value.PotionUseCount.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> k) => k.Value))
		{
			list.Add(new BarData
			{
				Key = item.Key,
				Label = CombatDataCollector.ResolvePotionName(item.Key),
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
