using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace SOCS.Code;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SOCS";

    public static void Initialize()
    {
        SocsRuntime.Initialize();
        Callable.From(StartInjectionLoop).CallDeferred();
    }

    private static void StartInjectionLoop()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.ProcessFrame += OnEveryFrame;
            Callable.From(SocsRuntime.ApplyStickyTimeScale).CallDeferred();
        }
        else
        {
            Callable.From(StartInjectionLoop).CallDeferred();
        }
    }

    private static void OnEveryFrame()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
        {
            return;
        }

        SocsRuntime.Tick();
    }
}
