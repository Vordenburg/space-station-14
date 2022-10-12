using Robust.Shared.GameStates;
using Content.Shared.Doors.Components;

namespace Content.Shared.Doors.Systems;

public sealed class SharedAirlockAssemblyPaintSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharedAirlockAssemblyPaintComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<SharedAirlockAssemblyPaintComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(EntityUid uid, SharedAirlockAssemblyPaintComponent paint, ref ComponentGetState args)
    {
        args.State = new SharedAirlockAssemblyPaintComponent.AirlockAssemblyPaintComponentState(paint);
    }

    private void OnHandleState(EntityUid uid, SharedAirlockAssemblyPaintComponent paint, ref ComponentHandleState args)
    {
        if (args.Current is not SharedAirlockAssemblyPaintComponent.AirlockAssemblyPaintComponentState state)
            return;

        paint.Style = state.Style;
    }
}

