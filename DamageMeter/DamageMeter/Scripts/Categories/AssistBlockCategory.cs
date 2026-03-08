using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class AssistBlockCategory : IStatCategory
{
	public string Name => I18n.CatAssistBlock;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		int num = CombatDataCollector.Players.Values.Sum((CombatDataCollector.PlayerStats p) => p.AssistBlockGiven);
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.AssistBlockGiven))
		{
			if (item.AssistBlockGiven > 0)
			{
				float value = ((num > 0) ? ((float)item.AssistBlockGiven / (float)num * 100f) : 0f);
				list.Add(new BarData
				{
					Key = item.Key,
					Label = item.Name,
					Value = item.AssistBlockGiven,
					DisplayText = $"{item.AssistBlockGiven} ({value:F1}%)"
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
		foreach (KeyValuePair<string, int> item in value.AssistBlockByRecipient.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> k) => k.Value))
		{
			string label = item.Key;
			if (CombatDataCollector.Players.TryGetValue(item.Key, out CombatDataCollector.PlayerStats value2))
			{
				label = value2.Name;
			}
			list.Add(new BarData
			{
				Key = item.Key,
				Label = label,
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
