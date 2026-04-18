using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DamageMeter.Scripts;

public static class CombatDataCollector
{
	public class CombatSegment
	{
		public Dictionary<string, PlayerStats> Players { get; } = new Dictionary<string, PlayerStats>();


		public int TurnCount { get; set; }

		public string EncounterKey { get; set; } = "";

	}

	public class PlayerStats
	{
		public string Name { get; set; } = "";


		public string Key { get; set; } = "";


		public string CharacterId { get; set; } = "";


		public Color CharacterColor { get; set; } = new Color(0.95f, 0.55f, 0.15f, 1f);


		public int DamageDealt { get; set; }

		public int RealDamageDealt { get; set; }

		public int DamageTaken { get; set; }

		public int BlockedByTarget { get; set; }

		public int OverkillDealt { get; set; }

		public int HitCount { get; set; }

		public Dictionary<int, int> DamagePerTurn { get; } = new Dictionary<int, int>();

		public Dictionary<int, int> RealDamagePerTurn { get; } = new Dictionary<int, int>();


		public Dictionary<string, int> DamageByCard { get; } = new Dictionary<string, int>();

		public Dictionary<string, int> RealDamageByCard { get; } = new Dictionary<string, int>();


		public Dictionary<string, int> DamageBySource { get; } = new Dictionary<string, int>();


		public int CardsPlayed { get; set; }

		public Dictionary<string, int> CardPlayCount { get; } = new Dictionary<string, int>();


		public Dictionary<CardType, int> CardTypeCount { get; } = new Dictionary<CardType, int>();


		public int TotalBlockGained { get; set; }

		public Dictionary<string, int> BlockByCard { get; } = new Dictionary<string, int>();


		public int TotalEnergySpent { get; set; }

		public int TotalEnergyWasted { get; set; }

		public Dictionary<string, int> EnergySpentByCard { get; } = new Dictionary<string, int>();


		public Dictionary<int, int> EnergyWastedPerTurn { get; } = new Dictionary<int, int>();


		public int PotionsUsed { get; set; }

		public Dictionary<string, int> PotionUseCount { get; } = new Dictionary<string, int>();


		public Dictionary<string, int> DebuffsApplied { get; } = new Dictionary<string, int>();


		public int CardsDrawn { get; set; }

		public int CardsDiscarded { get; set; }

		public int CardsExhausted { get; set; }

		public Dictionary<string, int> DrawCount { get; } = new Dictionary<string, int>();


		public Dictionary<string, int> DiscardCount { get; } = new Dictionary<string, int>();


		public Dictionary<string, int> ExhaustCount { get; } = new Dictionary<string, int>();


		public int MaxSingleHit { get; set; }

		public string MaxSingleHitCard { get; set; } = "";


		public int AssistDamage { get; set; }

		public Dictionary<string, int> AssistDamageByPower { get; } = new Dictionary<string, int>();


		public int AssistBlockGiven { get; set; }

		public Dictionary<string, int> AssistBlockByRecipient { get; } = new Dictionary<string, int>();


		public PlayerStats Clone()
		{
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			PlayerStats playerStats = new PlayerStats
			{
				Name = Name,
				Key = Key,
				CharacterId = CharacterId,
				CharacterColor = CharacterColor,
				DamageDealt = DamageDealt,
				RealDamageDealt = RealDamageDealt,
				DamageTaken = DamageTaken,
				BlockedByTarget = BlockedByTarget,
				OverkillDealt = OverkillDealt,
				HitCount = HitCount,
				CardsPlayed = CardsPlayed,
				TotalBlockGained = TotalBlockGained,
				TotalEnergySpent = TotalEnergySpent,
				TotalEnergyWasted = TotalEnergyWasted,
				PotionsUsed = PotionsUsed,
				CardsDrawn = CardsDrawn,
				CardsDiscarded = CardsDiscarded,
				CardsExhausted = CardsExhausted,
				MaxSingleHit = MaxSingleHit,
				MaxSingleHitCard = MaxSingleHitCard,
				AssistDamage = AssistDamage,
				AssistBlockGiven = AssistBlockGiven
			};
			CopyDict(DamagePerTurn, playerStats.DamagePerTurn);
			CopyDict(RealDamagePerTurn, playerStats.RealDamagePerTurn);
			CopyDict(DamageByCard, playerStats.DamageByCard);
			CopyDict(RealDamageByCard, playerStats.RealDamageByCard);
			CopyDict(DamageBySource, playerStats.DamageBySource);
			CopyDict(CardPlayCount, playerStats.CardPlayCount);
			CopyDict(CardTypeCount, playerStats.CardTypeCount);
			CopyDict(BlockByCard, playerStats.BlockByCard);
			CopyDict(EnergySpentByCard, playerStats.EnergySpentByCard);
			CopyDict(EnergyWastedPerTurn, playerStats.EnergyWastedPerTurn);
			CopyDict(PotionUseCount, playerStats.PotionUseCount);
			CopyDict(DebuffsApplied, playerStats.DebuffsApplied);
			CopyDict(DrawCount, playerStats.DrawCount);
			CopyDict(DiscardCount, playerStats.DiscardCount);
			CopyDict(ExhaustCount, playerStats.ExhaustCount);
			CopyDict(AssistDamageByPower, playerStats.AssistDamageByPower);
			CopyDict(AssistBlockByRecipient, playerStats.AssistBlockByRecipient);
			return playerStats;
		}

		public void MergeFrom(PlayerStats other)
		{
			DamageDealt += other.DamageDealt;
			RealDamageDealt += other.RealDamageDealt;
			DamageTaken += other.DamageTaken;
			BlockedByTarget += other.BlockedByTarget;
			OverkillDealt += other.OverkillDealt;
			HitCount += other.HitCount;
			CardsPlayed += other.CardsPlayed;
			TotalBlockGained += other.TotalBlockGained;
			TotalEnergySpent += other.TotalEnergySpent;
			TotalEnergyWasted += other.TotalEnergyWasted;
			PotionsUsed += other.PotionsUsed;
			CardsDrawn += other.CardsDrawn;
			CardsDiscarded += other.CardsDiscarded;
			CardsExhausted += other.CardsExhausted;
			if (other.MaxSingleHit > MaxSingleHit)
			{
				MaxSingleHit = other.MaxSingleHit;
				MaxSingleHitCard = other.MaxSingleHitCard;
			}
			MergeDict(other.DamagePerTurn, DamagePerTurn);
			MergeDict(other.RealDamagePerTurn, RealDamagePerTurn);
			MergeDict(other.DamageByCard, DamageByCard);
			MergeDict(other.RealDamageByCard, RealDamageByCard);
			MergeDict(other.DamageBySource, DamageBySource);
			MergeDict(other.CardPlayCount, CardPlayCount);
			MergeDict(other.CardTypeCount, CardTypeCount);
			MergeDict(other.BlockByCard, BlockByCard);
			MergeDict(other.EnergySpentByCard, EnergySpentByCard);
			MergeDict(other.EnergyWastedPerTurn, EnergyWastedPerTurn);
			MergeDict(other.PotionUseCount, PotionUseCount);
			MergeDict(other.DebuffsApplied, DebuffsApplied);
			MergeDict(other.DrawCount, DrawCount);
			MergeDict(other.DiscardCount, DiscardCount);
			MergeDict(other.ExhaustCount, ExhaustCount);
			AssistDamage += other.AssistDamage;
			AssistBlockGiven += other.AssistBlockGiven;
			MergeDict(other.AssistDamageByPower, AssistDamageByPower);
			MergeDict(other.AssistBlockByRecipient, AssistBlockByRecipient);
		}

		private static void CopyDict<TKey>(Dictionary<TKey, int> src, Dictionary<TKey, int> dst) where TKey : notnull
		{
			foreach (KeyValuePair<TKey, int> item in src)
			{
				dst[item.Key] = item.Value;
			}
		}

		private static void MergeDict<TKey>(Dictionary<TKey, int> src, Dictionary<TKey, int> dst) where TKey : notnull
		{
			foreach (KeyValuePair<TKey, int> item in src)
			{
				dst.TryGetValue(item.Key, out var value);
				dst[item.Key] = value + item.Value;
			}
		}
	}

	public enum CombatEventType
	{
		DamageDealt,
		DamageTaken,
		BlockGained,
		CardPlayed,
		PotionUsed,
		DebuffApplied
	}

	public record CombatEvent(CombatEventType Type, int Turn, string PlayerKey, string Label, int Value);

	public const int ViewCurrent = -1;

	public const int ViewOverall = -2;

	private static readonly Dictionary<string, PlayerStats> _players = new Dictionary<string, PlayerStats>();

	private static readonly List<CombatSegment> _segments = new List<CombatSegment>();

	private static bool _isTracking;

	private static int _currentTurn;

	private static int _viewIndex = -1;

	private static string _currentEncounterKey = "";

	private static Dictionary<string, PlayerStats>? _overallCache;

	private static bool _overallDirty = true;

	private static readonly List<CombatEvent> _eventLog = new List<CombatEvent>();

	private static readonly Dictionary<string, List<CombatEvent>> _deathLogs = new Dictionary<string, List<CombatEvent>>();

	private const int MaxEventLogSize = 200;

	private const int MaxDeathLogEntries = 16;

	public static bool IsTracking => _isTracking;

	public static int SegmentCount => _segments.Count;

	public static int ViewIndex => _viewIndex;

	public static int CurrentTurn => _currentTurn;

	public static int SelectedViewTurnCount
	{
		get
		{
			switch (_viewIndex)
			{
			case -1:
				return GetTurnCountForPlayers(_players, _currentTurn);
			case -2:
				return GetOverallTurnCount();
			default:
				if (_viewIndex >= 0 && _viewIndex < _segments.Count)
				{
					return GetTurnCountForPlayers(_segments[_viewIndex].Players, _segments[_viewIndex].TurnCount);
				}
				return GetTurnCountForPlayers(_players, _currentTurn);
			}
		}
	}

	public static IReadOnlyDictionary<string, PlayerStats> Players
	{
		get
		{
			switch (_viewIndex)
			{
			case -1:
				return _players;
			case -2:
				return GetOverallView();
			default:
				if (_viewIndex >= 0 && _viewIndex < _segments.Count)
				{
					return _segments[_viewIndex].Players;
				}
				return _players;
			}
		}
	}

	public static IReadOnlyCollection<string> DeadPlayers => _deathLogs.Keys;

	public static IReadOnlyList<CombatEvent> EventLog => _eventLog;

	public static event Action? StatsChanged;

	public static bool HasDeathLog(string playerKey)
	{
		return _deathLogs.ContainsKey(playerKey);
	}

	public static IReadOnlyList<CombatEvent> GetDeathLog(string playerKey)
	{
		if (!_deathLogs.TryGetValue(playerKey, out List<CombatEvent> value))
		{
			return Array.Empty<CombatEvent>();
		}
		return value;
	}

	public static IReadOnlyList<CombatEvent> GetPlayerEvents(string playerKey)
	{
		string playerKey2 = playerKey;
		return _eventLog.Where((CombatEvent e) => e.PlayerKey == playerKey2).ToList();
	}

	public static int GetPlayerEventCount(string playerKey)
	{
		string playerKey2 = playerKey;
		return _eventLog.Count((CombatEvent e) => e.PlayerKey == playerKey2);
	}

	public static void FinalizeCurrentCombatForRunReset()
	{
		if (!_players.Any() || !HasMeaningfulData(_players))
		{
			return;
		}

		MainFile.Log.Info($"Flushing current combat before run reset: {_currentEncounterKey} ({_currentTurn} turns)", 1);
		DamageMeterSettings.UpdateRecords(_players.Values);
	}

	public static void ArchiveAndStartNew(string encounterKey)
	{
		if (TryArchiveCurrentSegment())
		{
			_overallDirty = true;
		}
		_players.Clear();
		_eventLog.Clear();
		_deathLogs.Clear();
		_isTracking = true;
		_currentTurn = 1;
		_currentEncounterKey = encounterKey;
		_viewIndex = -1;
		_overallDirty = true;
	}

	public static void StopTracking()
	{
		_isTracking = false;
	}

	public static void ResetAll()
	{
		bool isTracking = _isTracking;
		_players.Clear();
		_segments.Clear();
		_eventLog.Clear();
		_deathLogs.Clear();
		_currentTurn = 1;
		_currentEncounterKey = "";
		_viewIndex = -1;
		_overallCache = null;
		_overallDirty = true;
		_isTracking = isTracking;
		MainFile.Log.Info($"All combat data reset (tracking={isTracking})", 1);
		NotifyChanged();
	}

	public static void CycleViewForward()
	{
		if (_segments.Count == 0)
		{
			_viewIndex = ((_viewIndex == -1) ? (-2) : (-1));
			NotifyChanged();
			return;
		}
		if (_viewIndex == -1)
		{
			_viewIndex = 0;
		}
		else if (_viewIndex == -2)
		{
			_viewIndex = -1;
		}
		else if (_viewIndex >= _segments.Count - 1)
		{
			_viewIndex = -2;
		}
		else
		{
			_viewIndex++;
		}
		NotifyChanged();
	}

	public static void CycleViewBackward()
	{
		if (_segments.Count == 0)
		{
			_viewIndex = ((_viewIndex == -1) ? (-2) : (-1));
			NotifyChanged();
			return;
		}
		if (_viewIndex == -1)
		{
			_viewIndex = -2;
		}
		else if (_viewIndex == -2)
		{
			_viewIndex = _segments.Count - 1;
		}
		else if (_viewIndex <= 0)
		{
			_viewIndex = -1;
		}
		else
		{
			_viewIndex--;
		}
		NotifyChanged();
	}

	public static void SetView(int viewIndex)
	{
		if (viewIndex == -1 || viewIndex == -2 || (viewIndex >= 0 && viewIndex < _segments.Count))
		{
			_viewIndex = viewIndex;
			NotifyChanged();
		}
	}

	public static string GetSegmentLabel(int index)
	{
		if (index < 0 || index >= _segments.Count)
		{
			return "";
		}
		string text = ResolveEncounterName(_segments[index].EncounterKey);
		string text2 = $"{I18n.SegmentFight} {index + 1}";
		if (!string.IsNullOrEmpty(text))
		{
			return text2 + ": " + text;
		}
		return text2;
	}

	public static string GetViewLabel()
	{
		if (_viewIndex == -1)
		{
			return I18n.SegmentCurrent;
		}
		if (_viewIndex == -2)
		{
			return I18n.SegmentOverall;
		}
		if (_viewIndex >= 0 && _viewIndex < _segments.Count)
		{
			string text = ResolveEncounterName(_segments[_viewIndex].EncounterKey);
			string text2 = $"{I18n.SegmentFight} {_viewIndex + 1}";
			if (!string.IsNullOrEmpty(text))
			{
				return text2 + ": " + text;
			}
			return text2;
		}
		return "";
	}

	public static void OnTurnStarted(CombatState state)
	{
		if (_isTracking)
		{
			_currentTurn = state.RoundNumber;
		}
	}

	public static void OnTurnEnded(CombatState state)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		if (!_isTracking || (int)state.CurrentSide != 1)
		{
			return;
		}
		foreach (Player player in state.Players)
		{
			PlayerCombatState playerCombatState = player.PlayerCombatState;
			if (playerCombatState != null)
			{
				int energy = playerCombatState.Energy;
				if (energy > 0)
				{
					PlayerStats orCreate = GetOrCreate(player.Creature);
					orCreate.TotalEnergyWasted += energy;
					orCreate.EnergyWastedPerTurn.TryGetValue(_currentTurn, out var value);
					orCreate.EnergyWastedPerTurn[_currentTurn] = value + energy;
				}
			}
		}
		NotifyChanged();
	}

	public static void RecordDamage(Creature? dealer, Creature receiver, DamageResult result, CardModel? cardSource)
	{
		if (!_isTracking)
		{
			return;
		}
		if (dealer != null && (dealer.IsPlayer || dealer.IsPet) && receiver.IsEnemy)
		{
			PlayerStats orCreate = GetOrCreate((Creature)(dealer.IsPlayer ? ((object)dealer) : ((object)dealer.PetOwner.Creature)));
			int totalOutput = result.BlockedDamage + result.UnblockedDamage;
			int realDamage = result.UnblockedDamage;
			int unblockedDamage = totalOutput;
			orCreate.DamageDealt += totalOutput;
			orCreate.RealDamageDealt += realDamage;
			orCreate.OverkillDealt += result.OverkillDamage;
			orCreate.BlockedByTarget += result.BlockedDamage;
			orCreate.HitCount++;
			orCreate.DamagePerTurn.TryGetValue(_currentTurn, out var value);
			orCreate.DamagePerTurn[_currentTurn] = value + totalOutput;
			orCreate.RealDamagePerTurn.TryGetValue(_currentTurn, out var value2);
			orCreate.RealDamagePerTurn[_currentTurn] = value2 + realDamage;
			object obj;
			if (!dealer.IsPet)
			{
				obj = ((cardSource != null) ? ((AbstractModel)cardSource).Id.Entry : null) ?? "Other";
			}
			else
			{
				MonsterModel monster = dealer.Monster;
				obj = ((monster != null) ? ((AbstractModel)monster).Id.Entry : null) ?? "Pet";
			}
			string text = (string)obj;
			orCreate.DamageByCard.TryGetValue(text, out var damageByCardValue);
			orCreate.DamageByCard[text] = damageByCardValue + totalOutput;
			orCreate.RealDamageByCard.TryGetValue(text, out var realDamageByCardValue);
			orCreate.RealDamageByCard[text] = realDamageByCardValue + realDamage;
			if (totalOutput > orCreate.MaxSingleHit)
			{
				orCreate.MaxSingleHit = totalOutput;
				orCreate.MaxSingleHitCard = text;
			}
			if (totalOutput > 0)
			{
				string value3 = (dealer.IsPet ? ResolveMonsterName(text) : ResolveCardName(text));
				LogEvent(new CombatEvent(CombatEventType.DamageDealt, _currentTurn, orCreate.Key, $"{value3} → {unblockedDamage}", unblockedDamage));
			}
			ComputeAndRecordAssists(dealer, receiver, result);
		}
		if (receiver.IsPlayer)
		{
			PlayerStats orCreate2 = GetOrCreate(receiver);
			int unblockedDamage2 = result.UnblockedDamage;
			orCreate2.DamageTaken += unblockedDamage2;
			object obj2;
			if (dealer == null || !dealer.IsMonster)
			{
				obj2 = null;
			}
			else
			{
				MonsterModel monster2 = dealer.Monster;
				obj2 = ((monster2 != null) ? ((AbstractModel)monster2).Id.Entry : null);
			}
			if (obj2 == null)
			{
				obj2 = "Unknown";
			}
			string key = (string)obj2;
			orCreate2.DamageBySource.TryGetValue(key, out var value4);
			orCreate2.DamageBySource[key] = value4 + unblockedDamage2;
			if (unblockedDamage2 > 0)
			{
				string value5 = ResolveMonsterName(key);
				LogEvent(new CombatEvent(CombatEventType.DamageTaken, _currentTurn, orCreate2.Key, $"{value5} → {unblockedDamage2}", unblockedDamage2));
			}
			if (result.WasTargetKilled)
			{
				SnapshotDeathLog(orCreate2.Key);
				MainFile.Log.Info($"Death detected for {orCreate2.Name} at turn {_currentTurn}", 1);
			}
		}
		NotifyChanged();
	}

	public static void RecordCardPlay(CardPlay cardPlay)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		if (!_isTracking)
		{
			return;
		}
		Player owner = cardPlay.Card.Owner;
		Creature val = ((owner != null) ? owner.Creature : null);
		if (val != null && val.IsPlayer)
		{
			PlayerStats orCreate = GetOrCreate(val);
			string entry = ((AbstractModel)cardPlay.Card).Id.Entry;
			CardType type = cardPlay.Card.Type;
			orCreate.CardsPlayed++;
			orCreate.CardPlayCount.TryGetValue(entry, out var value);
			orCreate.CardPlayCount[entry] = value + 1;
			orCreate.CardTypeCount.TryGetValue(type, out var value2);
			orCreate.CardTypeCount[type] = value2 + 1;
			ResourceInfo resources = cardPlay.Resources;
			int energySpent = resources.EnergySpent;
			orCreate.TotalEnergySpent += energySpent;
			if (energySpent > 0)
			{
				orCreate.EnergySpentByCard.TryGetValue(entry, out var value3);
				orCreate.EnergySpentByCard[entry] = value3 + energySpent;
			}
			string label = ResolveCardName(entry);
			LogEvent(new CombatEvent(CombatEventType.CardPlayed, _currentTurn, orCreate.Key, label, energySpent));
			NotifyChanged();
		}
	}

	public static void RecordBlockGained(Creature receiver, int amount, CardPlay? cardPlay)
	{
		if (_isTracking && receiver.IsPlayer && amount > 0)
		{
			PlayerStats orCreate = GetOrCreate(receiver);
			orCreate.TotalBlockGained += amount;
			string key = ((cardPlay != null) ? ((AbstractModel)cardPlay.Card).Id.Entry : null) ?? "Other";
			orCreate.BlockByCard.TryGetValue(key, out var value);
			orCreate.BlockByCard[key] = value + amount;
			object obj;
			if (cardPlay == null)
			{
				obj = null;
			}
			else
			{
				Player owner = cardPlay.Card.Owner;
				obj = ((owner != null) ? owner.Creature : null);
			}
			Creature val = (Creature)obj;
			if (val != null && val.IsPlayer && val != receiver)
			{
				PlayerStats orCreate2 = GetOrCreate(val);
				orCreate2.AssistBlockGiven += amount;
				string playerKey = GetPlayerKey(receiver);
				orCreate2.AssistBlockByRecipient.TryGetValue(playerKey, out var value2);
				orCreate2.AssistBlockByRecipient[playerKey] = value2 + amount;
			}
			string value3 = ResolveCardName(key);
			LogEvent(new CombatEvent(CombatEventType.BlockGained, _currentTurn, orCreate.Key, $"+{amount} ({value3})", amount));
			NotifyChanged();
		}
	}

	public static void RecordPotionUsed(PotionModel potion, Creature? target)
	{
		if (_isTracking)
		{
			Player owner = potion.Owner;
			Creature val = ((owner != null) ? owner.Creature : null);
			if (val != null && val.IsPlayer)
			{
				PlayerStats orCreate = GetOrCreate(val);
				string entry = ((AbstractModel)potion).Id.Entry;
				orCreate.PotionsUsed++;
				orCreate.PotionUseCount.TryGetValue(entry, out var value);
				orCreate.PotionUseCount[entry] = value + 1;
				string label = ResolvePotionName(entry);
				LogEvent(new CombatEvent(CombatEventType.PotionUsed, _currentTurn, orCreate.Key, label, 1));
				NotifyChanged();
			}
		}
	}

	public static void RecordPowerReceived(PowerModel power, decimal amount, Creature? applier)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		if (_isTracking && (int)power.Type == 2 && power.Owner.IsEnemy && applier != null && applier.IsPlayer)
		{
			PlayerStats orCreate = GetOrCreate(applier);
			string entry = ((AbstractModel)power).Id.Entry;
			int num = (int)Math.Max(1m, amount);
			orCreate.DebuffsApplied.TryGetValue(entry, out var value);
			orCreate.DebuffsApplied[entry] = value + num;
			string value2 = ResolvePowerName(entry);
			LogEvent(new CombatEvent(CombatEventType.DebuffApplied, _currentTurn, orCreate.Key, $"{value2} x{num}", num));
			NotifyChanged();
		}
	}

	private static void ComputeAndRecordAssists(Creature dealer, Creature receiver, DamageResult result)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		if (!((Enum)result.Props).HasFlag((Enum)(object)(ValueProp)8) || ((Enum)result.Props).HasFlag((Enum)(object)(ValueProp)4))
		{
			return;
		}
		int num = result.BlockedDamage + result.UnblockedDamage + result.OverkillDamage;
		if (num <= 0)
		{
			return;
		}
		object obj;
		if (!dealer.IsPlayer)
		{
			Player petOwner = dealer.PetOwner;
			obj = ((petOwner != null) ? petOwner.Creature : null);
		}
		else
		{
			obj = dealer;
		}
		Creature val = (Creature)obj;
		if (val == null)
		{
			return;
		}
		try
		{
			VulnerablePower power = receiver.GetPower<VulnerablePower>();
			if (((power != null) ? ((PowerModel)power).Applier : null) != null && ((PowerModel)power).Applier.IsPlayer && ((PowerModel)power).Applier != val)
			{
				int num2 = num / 3;
				if (num2 > 0)
				{
					AddAssistDamage(((PowerModel)power).Applier, "VULNERABLE_POWER", num2);
				}
			}
			foreach (FlankingPower powerInstance in receiver.GetPowerInstances<FlankingPower>())
			{
				if (((PowerModel)powerInstance).Applier == null || !((PowerModel)powerInstance).Applier.IsPlayer || ((PowerModel)powerInstance).Applier == val)
				{
					continue;
				}
				int amount = ((PowerModel)powerInstance).Amount;
				if (amount > 1)
				{
					int num3 = num - num / amount;
					if (num3 > 0)
					{
						AddAssistDamage(((PowerModel)powerInstance).Applier, "FLANKING_POWER", num3);
					}
				}
			}
		}
		catch (Exception value)
		{
			MainFile.Log.Error($"ComputeAndRecordAssists failed: {value}", 1);
		}
	}

	private static void AddAssistDamage(Creature assister, string powerKey, int amount)
	{
		PlayerStats orCreate = GetOrCreate(assister);
		orCreate.AssistDamage += amount;
		orCreate.AssistDamageByPower.TryGetValue(powerKey, out var value);
		orCreate.AssistDamageByPower[powerKey] = value + amount;
	}

	public static void RecordCardDrawn(CardModel card)
	{
		if (_isTracking)
		{
			Player owner = card.Owner;
			Creature val = ((owner != null) ? owner.Creature : null);
			if (val != null && val.IsPlayer)
			{
				PlayerStats orCreate = GetOrCreate(val);
				string entry = ((AbstractModel)card).Id.Entry;
				orCreate.CardsDrawn++;
				orCreate.DrawCount.TryGetValue(entry, out var value);
				orCreate.DrawCount[entry] = value + 1;
				NotifyChanged();
			}
		}
	}

	public static void RecordCardDiscarded(CardModel card)
	{
		if (_isTracking)
		{
			Player owner = card.Owner;
			Creature val = ((owner != null) ? owner.Creature : null);
			if (val != null && val.IsPlayer)
			{
				PlayerStats orCreate = GetOrCreate(val);
				string entry = ((AbstractModel)card).Id.Entry;
				orCreate.CardsDiscarded++;
				orCreate.DiscardCount.TryGetValue(entry, out var value);
				orCreate.DiscardCount[entry] = value + 1;
				NotifyChanged();
			}
		}
	}

	public static void RecordCardExhausted(CardModel card)
	{
		if (_isTracking)
		{
			Player owner = card.Owner;
			Creature val = ((owner != null) ? owner.Creature : null);
			if (val != null && val.IsPlayer)
			{
				PlayerStats orCreate = GetOrCreate(val);
				string entry = ((AbstractModel)card).Id.Entry;
				orCreate.CardsExhausted++;
				orCreate.ExhaustCount.TryGetValue(entry, out var value);
				orCreate.ExhaustCount[entry] = value + 1;
				NotifyChanged();
			}
		}
	}

	private static void LogEvent(CombatEvent evt)
	{
		_eventLog.Add(evt);
		if (_eventLog.Count > 200)
		{
			_eventLog.RemoveAt(0);
		}
	}

	private static void SnapshotDeathLog(string playerKey)
	{
		string playerKey2 = playerKey;
		if (!_deathLogs.ContainsKey(playerKey2))
		{
			List<CombatEvent> value = _eventLog.Where((CombatEvent e) => e.PlayerKey == playerKey2).TakeLast(MaxDeathLogEntries).ToList();
			_deathLogs[playerKey2] = value;
		}
	}

	private static string GetPlayerKey(Creature creature)
	{
		Player player = creature.Player;
		return ((player != null) ? player.NetId.ToString() : null) ?? creature.Name;
	}

	public static string ResolveCardName(string key)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		if (key == "Other")
		{
			return key;
		}
		try
		{
			LocString val = new LocString("cards", key + ".title");
			if (val.Exists())
			{
				return val.GetFormattedText();
			}
		}
		catch
		{
		}
		try
		{
			LocString val2 = new LocString("monsters", key + ".name");
			if (val2.Exists())
			{
				return val2.GetFormattedText();
			}
		}
		catch
		{
		}
		return key;
	}

	public static string ResolvePotionName(string key)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		try
		{
			LocString val = new LocString("potions", key + ".title");
			if (val.Exists())
			{
				return val.GetFormattedText();
			}
		}
		catch
		{
		}
		return key;
	}

	public static string ResolvePowerName(string key)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		try
		{
			LocString val = new LocString("powers", key + ".title");
			if (val.Exists())
			{
				return val.GetFormattedText();
			}
		}
		catch
		{
		}
		return key;
	}

	public static string ResolveMonsterName(string key)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		if (key == "Unknown")
		{
			return key;
		}
		try
		{
			LocString val = new LocString("monsters", key + ".name");
			if (val.Exists())
			{
				return val.GetFormattedText();
			}
		}
		catch
		{
		}
		return key;
	}

	public static string ResolveEncounterName(string key)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		if (string.IsNullOrEmpty(key))
		{
			return "";
		}
		try
		{
			LocString val = new LocString("encounters", key + ".title");
			if (val.Exists())
			{
				return val.GetFormattedText();
			}
		}
		catch
		{
		}
		return key;
	}

	private static int GetOverallTurnCount()
	{
		int num = _segments.Sum((CombatSegment segment) => GetTurnCountForPlayers(segment.Players, segment.TurnCount));
		return num + GetTurnCountForPlayers(_players, _currentTurn);
	}

	private static int GetTurnCountForPlayers(IReadOnlyDictionary<string, PlayerStats> players, int rawTurnCount)
	{
		if (!HasMeaningfulData(players))
		{
			return 0;
		}
		return Math.Max(1, rawTurnCount);
	}

	private static bool TryArchiveCurrentSegment()
	{
		if (_players.Count == 0 || !HasMeaningfulData(_players))
		{
			return false;
		}

		CombatSegment combatSegment = new CombatSegment
		{
			TurnCount = Math.Max(1, _currentTurn),
			EncounterKey = _currentEncounterKey
		};
		foreach (KeyValuePair<string, PlayerStats> player in _players)
		{
			combatSegment.Players[player.Key] = player.Value.Clone();
		}

		_segments.Add(combatSegment);
		MainFile.Log.Info($"Archived segment #{_segments.Count}: {_currentEncounterKey} ({combatSegment.TurnCount} turns)", 1);
		DamageMeterSettings.UpdateRecords(_players.Values);
		return true;
	}

	private static bool HasMeaningfulData()
	{
		return HasMeaningfulData(_players);
	}

	private static bool HasMeaningfulData(IReadOnlyDictionary<string, PlayerStats> players)
	{
		return players.Values.Any((PlayerStats p) => p.DamageDealt > 0 || p.DamageTaken > 0 || p.CardsPlayed > 0 || p.TotalBlockGained > 0 || p.PotionsUsed > 0 || p.CardsDrawn > 0);
	}

	private static void NotifyChanged()
	{
		_overallDirty = true;
		CombatDataCollector.StatsChanged?.Invoke();
	}

	private static IReadOnlyDictionary<string, PlayerStats> GetOverallView()
	{
		if (_overallCache == null || _overallDirty)
		{
			_overallCache = BuildOverallView();
			_overallDirty = false;
		}
		return _overallCache;
	}

	private static Dictionary<string, PlayerStats> BuildOverallView()
	{
		Dictionary<string, PlayerStats> dictionary = new Dictionary<string, PlayerStats>();
		foreach (CombatSegment segment in _segments)
		{
			MergePlayersInto(segment.Players, dictionary);
		}
		MergePlayersInto(_players, dictionary);
		return dictionary;
	}

	private static void MergePlayersInto(Dictionary<string, PlayerStats> source, Dictionary<string, PlayerStats> target)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		foreach (KeyValuePair<string, PlayerStats> item in source)
		{
			if (!target.TryGetValue(item.Key, out PlayerStats value))
			{
				value = new PlayerStats
				{
					Name = item.Value.Name,
					Key = item.Value.Key,
					CharacterId = item.Value.CharacterId,
					CharacterColor = item.Value.CharacterColor
				};
				target[item.Key] = value;
			}
			value.MergeFrom(item.Value);
		}
	}

	private static PlayerStats GetOrCreate(Creature creature)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		string playerKey = GetPlayerKey(creature);
		if (!_players.TryGetValue(playerKey, out PlayerStats value))
		{
			string playerDisplayName = GetPlayerDisplayName(creature);
			Player player = creature.Player;
			object obj;
			if (player == null)
			{
				obj = null;
			}
			else
			{
				CharacterModel character = player.Character;
				obj = ((character != null) ? ((AbstractModel)character).Id.Entry : null);
			}
			if (obj == null)
			{
				obj = "";
			}
			string characterId = (string)obj;
			Player player2 = creature.Player;
			Color? obj2;
			if (player2 == null)
			{
				obj2 = null;
			}
			else
			{
				CharacterModel character2 = player2.Character;
				obj2 = ((character2 != null) ? new Color?(character2.NameColor) : null);
			}
			Color characterColor = obj2 ?? new Color(0.95f, 0.55f, 0.15f, 1f);
			value = new PlayerStats
			{
				Name = playerDisplayName,
				Key = playerKey,
				CharacterId = characterId,
				CharacterColor = characterColor
			};
			_players[playerKey] = value;
		}
		return value;
	}

	private static string GetPlayerDisplayName(Creature creature)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Player player = creature.Player;
			if (player != null)
			{
				RunManager instance = RunManager.Instance;
				PlatformType? obj;
				if (instance == null)
				{
					obj = null;
				}
				else
				{
					INetGameService netService = instance.NetService;
					obj = ((netService != null) ? new PlatformType?(netService.Platform) : null);
				}
				PlatformType? val = obj;
				if (val.HasValue)
				{
					ulong num = (RunManager.Instance.IsSinglePlayerOrFakeMultiplayer ? PlatformUtil.GetLocalPlayerId(val.Value) : player.NetId);
					string playerName = PlatformUtil.GetPlayerName(val.Value, num);
					if (!string.IsNullOrEmpty(playerName))
					{
						return playerName;
					}
				}
			}
			return creature.Name;
		}
		catch
		{
			Player player2 = creature.Player;
			object obj2;
			if (player2 == null)
			{
				obj2 = null;
			}
			else
			{
				CharacterModel character = player2.Character;
				obj2 = ((character != null) ? ((AbstractModel)character).Id.Entry : null);
			}
			if (obj2 == null)
			{
				obj2 = "Unknown";
			}
			return (string)obj2;
		}
	}
}
