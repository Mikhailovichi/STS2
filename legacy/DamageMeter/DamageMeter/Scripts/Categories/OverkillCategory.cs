using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class OverkillCategory : IStatCategory
{
	public string Name => I18n.CatOverkill;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.OverkillDealt))
		{
			if (item.OverkillDealt > 0)
			{
				list.Add(new BarData
				{
					Key = item.Key,
					Label = item.Name,
					Value = item.OverkillDealt
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
		if (value.DamageDealt > 0)
		{
			list.Add(new BarData
			{
				Key = "effective",
				Label = I18n.CatDamageDealt,
				Value = value.DamageDealt
			});
		}
		if (value.RealDamageDealt > 0)
		{
			list.Add(new BarData
			{
				Key = "real",
				Label = I18n.CatRealDamage,
				Value = value.RealDamageDealt
			});
		}
		if (value.OverkillDealt > 0)
		{
			list.Add(new BarData
			{
				Key = "overkill",
				Label = I18n.CatOverkill,
				Value = value.OverkillDealt
			});
		}
		if (value.BlockedByTarget > 0)
		{
			list.Add(new BarData
			{
				Key = "blocked",
				Label = I18n.Blocked,
				Value = value.BlockedByTarget
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
