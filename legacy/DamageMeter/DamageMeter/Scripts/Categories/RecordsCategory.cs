using System;
using System.Collections.Generic;

namespace DamageMeter.Scripts.Categories;

public class RecordsCategory : IStatCategory
{
	public string Name => I18n.CatRecords;

	public bool HasDetail => false;

	public List<BarData> GetPlayerBars()
	{
		DamageMeterSettings.PersonalRecords records = DamageMeterSettings.Records;
		List<BarData> list = new List<BarData>();
		if (records.HighestHit > 0)
		{
			string text = records.HighestHit.ToString();
			if (!string.IsNullOrEmpty(records.HighestHitCard))
			{
				text = text + " (" + CombatDataCollector.ResolveCardName(records.HighestHitCard) + ")";
			}
			list.Add(new BarData
			{
				Key = "highest_hit",
				Label = I18n.RecHighestHit,
				Value = records.HighestHit,
				DisplayText = text
			});
		}
		if (records.MostFightDamage > 0)
		{
			list.Add(new BarData
			{
				Key = "most_fight_dmg",
				Label = I18n.RecMostFightDmg,
				Value = records.MostFightDamage,
				DisplayText = records.MostFightDamage.ToString()
			});
		}
		if (records.BestTurnDamage > 0)
		{
			list.Add(new BarData
			{
				Key = "best_turn_dmg",
				Label = I18n.RecBestTurnDmg,
				Value = records.BestTurnDamage,
				DisplayText = records.BestTurnDamage.ToString()
			});
		}
		if (records.MostCardsPlayed > 0)
		{
			list.Add(new BarData
			{
				Key = "most_cards",
				Label = I18n.RecMostCards,
				Value = records.MostCardsPlayed,
				DisplayText = records.MostCardsPlayed.ToString()
			});
		}
		if (records.MostBlockGained > 0)
		{
			list.Add(new BarData
			{
				Key = "most_block",
				Label = I18n.RecMostBlock,
				Value = records.MostBlockGained,
				DisplayText = records.MostBlockGained.ToString()
			});
		}
		if (records.TotalDamage > 0)
		{
			string displayText = ((records.TotalDamage >= 1000000) ? $"{(double)records.TotalDamage / 1000000.0:F1}M" : ((records.TotalDamage >= 1000) ? $"{(double)records.TotalDamage / 1000.0:F1}K" : records.TotalDamage.ToString()));
			list.Add(new BarData
			{
				Key = "total_dmg",
				Label = I18n.RecTotalDamage,
				Value = (int)Math.Min(records.TotalDamage, 2147483647L),
				DisplayText = displayText
			});
		}
		if (records.TotalFights > 0)
		{
			list.Add(new BarData
			{
				Key = "total_fights",
				Label = I18n.RecTotalFights,
				Value = records.TotalFights,
				DisplayText = records.TotalFights.ToString()
			});
		}
		if (list.Count == 0)
		{
			list.Add(new BarData
			{
				Key = "none",
				Label = I18n.RecNoRecords,
				Value = 0,
				DisplayText = ""
			});
		}
		return list;
	}

	public List<BarData> GetDetailBars(string playerKey)
	{
		return new List<BarData>();
	}

	public string GetDetailTitle(string playerKey)
	{
		return I18n.CatRecords;
	}
}
