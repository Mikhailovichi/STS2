using System.Collections.Generic;
using Godot;

namespace DamageMeter.Scripts;

public sealed class TrendChartData
{
	public string Title { get; set; } = "";

	public string Summary { get; set; } = "";

	public List<int> Values { get; } = new List<int>();

	public Color LineColor { get; set; } = Colors.White;
}

public interface ITrendChartCategory
{
	TrendChartData? GetDashboardTrend(string? playerKey);
}
