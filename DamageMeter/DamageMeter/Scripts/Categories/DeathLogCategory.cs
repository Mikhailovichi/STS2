using System;
using System.Collections.Generic;

namespace DamageMeter.Scripts.Categories;

public class DeathLogCategory : IStatCategory
{
	public string Name => I18n.CatDeathLog;

	public List<BarData> GetPlayerBars()
	{
		List<BarData> list = new List<BarData>();
		if (CombatDataCollector.DeadPlayers.Count == 0)
		{
			foreach (CombatDataCollector.PlayerStats value in CombatDataCollector.Players.Values)
			{
				list.Add(new BarData
				{
					Key = value.Key,
					Label = value.Name,
					Value = 0,
					DisplayText = I18n.Alive
				});
			}
			return list;
		}
		foreach (CombatDataCollector.PlayerStats value2 in CombatDataCollector.Players.Values)
		{
			bool flag = CombatDataCollector.HasDeathLog(value2.Key);
			list.Add(new BarData
			{
				Key = value2.Key,
				Label = value2.Name,
				Value = (flag ? 1 : 0),
				DisplayText = (flag ? I18n.Dead : I18n.Alive)
			});
		}
		return list;
	}

	public List<BarData> GetDetailBars(string playerKey)
	{
		IReadOnlyList<CombatDataCollector.CombatEvent> deathLog = CombatDataCollector.GetDeathLog(playerKey);
		if (deathLog.Count == 0)
		{
			return new List<BarData>();
		}
		List<BarData> list = new List<BarData>();
		foreach (CombatDataCollector.CombatEvent item in deathLog)
		{
			string text = item.Type switch
			{
				CombatDataCollector.CombatEventType.DamageTaken => $"T{item.Turn} [-]",
				CombatDataCollector.CombatEventType.DamageDealt => $"T{item.Turn} [+]",
				CombatDataCollector.CombatEventType.BlockGained => $"T{item.Turn} [B]",
				CombatDataCollector.CombatEventType.CardPlayed => $"T{item.Turn} [C]",
				CombatDataCollector.CombatEventType.PotionUsed => $"T{item.Turn} [P]",
				CombatDataCollector.CombatEventType.DebuffApplied => $"T{item.Turn} [D]",
				_ => $"T{item.Turn}"
			};
			list.Add(new BarData
			{
				Key = $"evt_{list.Count}",
				Label = text + " " + item.Label,
				Value = Math.Max(1, item.Value),
				DisplayText = ((item.Value > 0) ? item.Value.ToString() : "")
			});
		}
		return list;
	}

	public string GetDetailTitle(string playerKey)
	{
		if (CombatDataCollector.Players.TryGetValue(playerKey, out CombatDataCollector.PlayerStats value))
		{
			return value.Name + " - " + I18n.DeathLog;
		}
		return I18n.DeathLog;
	}
}
