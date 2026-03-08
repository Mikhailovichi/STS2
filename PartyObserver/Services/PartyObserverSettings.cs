using Godot;

namespace PartyObserver.Services;

internal sealed class PartyObserverSettings
{
    public bool Enabled { get; set; } = true;

    public float PanelOpacity { get; set; } = 0.9f;

    public float PanelPositionX { get; set; } = -1f;

    public float PanelPositionY { get; set; } = -1f;

    public void Normalize()
    {
        PanelOpacity = Mathf.Clamp(PanelOpacity, 0.25f, 1f);
    }
}
