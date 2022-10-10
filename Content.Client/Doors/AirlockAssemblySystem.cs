using Robust.Shared.Containers;
using Robust.Client.GameObjects;
using Content.Shared.Doors.Components;

namespace Content.Client.Doors.Systems
{
    // This component and its system exist purely to help modify an airlock
    // assembly's sprite when it is deconstructed from the airlock.
    // This way, it'll have the same base sprite as the original airlock.
    //
    // If there was a way to simultaneously touch the newly created airlock
    // assembly and the deconstructed airlock, this would not be necessary.

    public sealed class AirlockAssemblySystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SharedAirlockAssemblyComponent, EntInsertedIntoContainerMessage>(OnInserted);
        }

        private void OnInserted(EntityUid uid, SharedAirlockAssemblyComponent component, ContainerModifiedMessage args)
        {
            if (args.Container.ID == "paint"
                && TryComp<SpriteComponent>(uid, out var assemblySpriteComponent)
                && TryComp<SpriteComponent>(args.Entity, out var paintSpriteComponent))
            {
                assemblySpriteComponent.BaseRSI = paintSpriteComponent.BaseRSI;
            }
        }
    }
}
