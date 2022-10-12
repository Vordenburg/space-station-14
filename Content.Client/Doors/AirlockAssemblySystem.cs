using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Client.GameObjects;
using Content.Shared.AirlockPainter.Prototypes;
using Content.Shared.Doors.Components;

namespace Content.Client.Doors.Systems
{
    // This component and its system exist purely to help modify an airlock
    // assembly's sprite when it is deconstructed from the airlock.
    // This way, it'll have the same base sprite as the original airlock.
    //
    // If there was a way to simultaneously touch the newly created airlock
    // assembly and the deconstructed airlock, this would not be necessary.
    //
    // Note: Construction actions will not work, because ChangeEntity has
    // performActions set to false. A completed action will not work either,
    // because that runs on the old entity and does not include the new one.
    //
    // It's a vexing problem.

    public sealed class AirlockAssemblySystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SharedAirlockAssemblyComponent, EntInsertedIntoContainerMessage>(OnInserted);
        }

        private void OnInserted(EntityUid uid, SharedAirlockAssemblyComponent component, ContainerModifiedMessage args)
        {
            if (args.Container.ID == "paint"
                && TryComp(args.Entity, out SharedAirlockAssemblyPaintComponent? paintComponent)
                && paintComponent.Style != null
                // Assuming Standard here for now.
                && _prototypeManager.TryIndex("Standard", out AirlockGroupPrototype? airlockGroup)
                && airlockGroup.StylePaths.TryGetValue(paintComponent.Style, out string? rsiPath)
                && TryComp<SpriteComponent>(uid, out var assemblySpriteComponent)
                && rsiPath != null)
            {
                assemblySpriteComponent.LayerSetRSI(0, rsiPath);
            }
        }
    }
}
