using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class DebuffsCategory : IStatCategory
{
	public string Name => I18n.CatDebuffs;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
		{
			int num = value.DebuffsApplied.Values.Sum();
			if (num > 0)
			{
				list.Add(new BarData
				{
					Key = value.Key,
					Label = value.Name,
					Value = num
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
		foreach (KeyValuePair<string, int> item in value.DebuffsApplied.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> k) => k.Value))
		{
			list.Add(new BarData
			{
				Key = item.Key,
				Label = CombatDataCollector.ResolvePowerName(item.Key),
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
