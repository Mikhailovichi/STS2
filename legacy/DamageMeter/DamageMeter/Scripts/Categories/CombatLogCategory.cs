using System;
using System.Collections.Generic;
using System.Linq;

namespace DamageMeter.Scripts.Categories;

public class CombatLogCategory : IStatCategory
{
	public string Name => I18n.CatCombatLog;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
		{
			int playerEventCount = CombatDataCollector.GetPlayerEventCount(value.Key);
			list.Add(new BarData
			{
				Key = value.Key,
				Label = value.Name,
				Value = playerEventCount,
				DisplayText = $"{playerEventCount} {I18n.Events}"
			});
		}
		return list.OrderByDescending((BarData b) => b.Value).ToList();
	}

	public List<BarData> GetDetailBars(string playerKey)
	{
		IReadOnlyList<CombatDataCollector.CombatEvent> playerEvents = CombatDataCollector.GetPlayerEvents(playerKey);
		List<BarData> list = new List<BarData>();
		int num = Math.Max(0, playerEvents.Count - 50);
		for (int num2 = playerEvents.Count - 1; num2 >= num; num2--)
		{
			CombatDataCollector.CombatEvent combatEvent = playerEvents[num2];
			string text = combatEvent.Type switch
			{
				CombatDataCollector.CombatEventType.DamageDealt => $"T{combatEvent.Turn} [+]",
				CombatDataCollector.CombatEventType.DamageTaken => $"T{combatEvent.Turn} [-]",
				CombatDataCollector.CombatEventType.BlockGained => $"T{combatEvent.Turn} [B]",
				CombatDataCollector.CombatEventType.CardPlayed => $"T{combatEvent.Turn} [C]",
				CombatDataCollector.CombatEventType.PotionUsed => $"T{combatEvent.Turn} [P]",
				CombatDataCollector.CombatEventType.DebuffApplied => $"T{combatEvent.Turn} [D]",
				_ => $"T{combatEvent.Turn}"
			};
			list.Add(new BarData
			{
				Key = $"evt_{list.Count}",
				Label = text + " " + combatEvent.Label,
				Value = Math.Max(1, combatEvent.Value),
				DisplayText = ((combatEvent.Value > 0) ? combatEvent.Value.ToString() : "")
			});
		}
		return list;
	}

	public string GetDetailTitle(string playerKey)
	{
		if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return value.Name + " - " + I18n.CatCombatLog;
		}
		return I18n.CatCombatLog;
	}
}
