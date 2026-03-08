using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class AssistDamageCategory : IStatCategory
{
	public string Name => I18n.CatAssistDamage;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		int num = CombatDataCollector.Players.Values.Sum((CombatDataCollector.PlayerStats p) => p.AssistDamage);
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.AssistDamage))
		{
			if (item.AssistDamage > 0)
			{
				float value = ((num > 0) ? ((float)item.AssistDamage / (float)num * 100f) : 0f);
				list.Add(new BarData
				{
					Key = item.Key,
					Label = item.Name,
					Value = item.AssistDamage,
					DisplayText = $"{item.AssistDamage} ({value:F1}%)"
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
		foreach (KeyValuePair<string, int> item in value.AssistDamageByPower.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> k) => k.Value))
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
