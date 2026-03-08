using System.Collections.Generic;

namespace DamageMeter.Scripts;

public interface IStatCategory
{
	string Name { get; }

	bool HasDetail => true;

	List<BarData> GetPlayerBars();

	List<BarData> GetDetailBars(string playerKey);

	string GetDetailTitle(string playerKey);
}
