using Godot;

namespace DamageMeter.Scripts;

public class InputHandler : Node
{
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && (long)keyEvent.Keycode == 4194338)
		{
			DamageMeterUI.ToggleVisibility();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey dashboardClose && dashboardClose.Pressed && (long)dashboardClose.Keycode == 4194305 && DamageMeterUI.IsDashboardVisible)
		{
			DamageMeterUI.CloseDashboard();
			GetViewport().SetInputAsHandled();
		}
	}
}
