using Godot;
using MegaCrit.Sts2.Core.Runs;
using CombatQuill.Services;

namespace CombatQuill.Runtime;

internal sealed partial class CombatQuillRuntimeBootstrapper : Node
{
    private const string BootstrapperNodeName = "CombatQuillRuntimeBootstrapper";
    private bool _hadRunContext;

    public static void EnsureInstalled()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is null)
        {
            GD.PrintErr($"{MainFile.ModId}: could not install runtime bootstrapper because SceneTree is unavailable");
            return;
        }

        if (tree.Root.GetNodeOrNull<CombatQuillRuntimeBootstrapper>(BootstrapperNodeName) is not null)
        {
            return;
        }

        var bootstrapper = new CombatQuillRuntimeBootstrapper
        {
            Name = BootstrapperNodeName
        };

        Callable.From(() => tree.Root.AddChild(bootstrapper)).CallDeferred();
    }

    public override void _Ready()
    {
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        SyncRuntimeState();
    }

    private void SyncRuntimeState()
    {
        var runManager = RunManager.Instance;
        if (runManager is null)
        {
            return;
        }

        if (!runManager.IsInProgress)
        {
            if (_hadRunContext)
            {
                CombatQuillService.InitializeRunContext();
                _hadRunContext = false;
            }

            return;
        }

        _hadRunContext = true;
        CombatQuillService.TryEnsureOverlaysForCurrentRun();
    }
}
