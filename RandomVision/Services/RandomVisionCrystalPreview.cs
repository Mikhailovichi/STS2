using Godot;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;

namespace RandomVision.Services;

internal static class RandomVisionCrystalPreview
{
    private const string OverlayName = "RandomVisionCrystalPreview";
    private const float CellSize = 57f;

    public static void Remove(NCrystalSphereScreen screen)
    {
        MainFile.LogInfo("crystal-preview remove");
        screen.GetNodeOrNull<Control>(OverlayName)?.QueueFree();

        var itemsContainer = screen.GetNodeOrNull<Control>("%Items");
        itemsContainer?.GetNodeOrNull<Control>(OverlayName)?.QueueFree();
        (itemsContainer?.GetParent() as Control)?.GetNodeOrNull<Control>(OverlayName)?.QueueFree();
    }

    public static void AttachIfNeeded(NCrystalSphereScreen screen, CrystalSphereMinigame minigame)
    {
        var itemsContainer = screen.GetNodeOrNull<Control>("%Items");
        // Keep the preview inside the item container so it follows the screen's lifecycle and visibility.
        var host = itemsContainer ?? screen;
        if (host.GetNodeOrNull<Control>(OverlayName) is not null)
        {
            MainFile.LogInfo($"crystal-preview skip-existing items={minigame.Items.Count} grid={minigame.GridSize}");
            return;
        }

        MainFile.LogInfo($"crystal-preview attach-start items={minigame.Items.Count} grid={minigame.GridSize}");

        var overlay = new Control
        {
            Name = OverlayName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 160,
            Position = Vector2.Zero,
            Size = host.Size
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect, keepOffsets: false);

        host.AddChild(overlay);

        var gridOffset = Vector2.One * (-(CellSize * minigame.GridSize.X) * 0.5f);
        var previewCount = 0;
        foreach (var item in minigame.Items)
        {
            var preview = NCrystalSphereItem.Create(item);
            if (preview is null)
            {
                MainFile.LogInfo($"crystal-preview item-skipped position={item.Position} size={item.Size}");
                continue;
            }

            preview.Size = new Vector2(item.Size.X * CellSize, item.Size.Y * CellSize);
            preview.Position = gridOffset + (CellSize * new Vector2(item.Position.X, item.Position.Y));
            preview.MouseFilter = Control.MouseFilterEnum.Ignore;
            preview.FocusMode = Control.FocusModeEnum.None;
            preview.Modulate = new Color(1f, 1f, 1f, 0.28f);
            overlay.AddChild(preview);
            previewCount++;
            MainFile.LogInfo($"crystal-preview item-added index={previewCount - 1} position={item.Position} size={item.Size}");
        }

        overlay.AddChild(CreateTag(gridOffset));
        MainFile.LogInfo($"crystal-preview attach-done previews={previewCount}");
    }

    private static Control CreateTag(Vector2 gridOffset)
    {
        var tag = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = gridOffset + new Vector2(18f, 14f),
            Size = new Vector2(132f, 34f)
        };
        tag.AddThemeStyleboxOverride("panel", CreateTagStyle());

        var label = new Label
        {
            Text = RandomVisionI18n.Pick("Foresight", "透视预览"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect, keepOffsets: false);
        label.AddThemeColorOverride("font_color", new Color("FFF5D6"));
        label.AddThemeFontSizeOverride("font_size", 14);

        tag.AddChild(label);
        return tag;
    }

    private static StyleBoxFlat CreateTagStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.16f, 0.22f, 0.9f),
            BorderColor = new Color("8FB8D8")
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        return style;
    }
}
