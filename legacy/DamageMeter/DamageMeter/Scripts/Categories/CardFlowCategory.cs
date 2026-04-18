using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class CardFlowCategory : IStatCategory
{
	public string Name => I18n.CatCardFlow;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats item in CombatDataCollector.Players.Values.OrderByDescending((CombatDataCollector.PlayerStats p) => p.CardsDrawn))
		{
			int num = item.CardsDrawn + item.CardsDiscarded + item.CardsExhausted;
			if (num > 0)
			{
				list.Add(new BarData
				{
					Key = item.Key,
					Label = item.Name,
					Value = num,
					DisplayText = $"{item.CardsDrawn}D {item.CardsDiscarded}d {item.CardsExhausted}E"
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
		if (value.CardsDrawn > 0)
		{
			list.Add(new BarData
			{
				Key = "drawn",
				Label = I18n.Drawn,
				Value = value.CardsDrawn
			});
		}
		if (value.CardsDiscarded > 0)
		{
			list.Add(new BarData
			{
				Key = "discarded",
				Label = I18n.Discarded,
				Value = value.CardsDiscarded
			});
		}
		if (value.CardsExhausted > 0)
		{
			list.Add(new BarData
			{
				Key = "exhausted",
				Label = I18n.Exhausted,
				Value = value.CardsExhausted
			});
		}
		foreach (KeyValuePair<string, int> item in value.ExhaustCount.OrderByDescending<KeyValuePair<string, int>, int>((KeyValuePair<string, int> k) => k.Value).Take(5))
		{
			list.Add(new BarData
			{
				Key = "exhaust_" + item.Key,
				Label = "  " + CombatDataCollector.ResolveCardName(item.Key),
				Value = item.Value,
				DisplayText = $"{item.Value}x {I18n.Exhausted}"
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
