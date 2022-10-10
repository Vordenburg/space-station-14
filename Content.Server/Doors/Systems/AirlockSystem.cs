using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Doors.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Server.Wires;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Doors.Systems
{
    public sealed class AirlockSystem : SharedAirlockSystem
    {
        [Dependency] private readonly WiresSystem _wiresSystem = default!;
        [Dependency] private readonly PowerReceiverSystem _power = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly ConstructionSystem _constructionSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<AirlockComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<AirlockComponent, DoorStateChangedEvent>(OnStateChanged);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpened);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorDeniedEvent>(OnBeforeDoorDenied);
            SubscribeLocalEvent<AirlockComponent, ActivateInWorldEvent>(OnActivate, before: new [] {typeof(DoorSystem)});
            SubscribeLocalEvent<AirlockComponent, DoorGetPryTimeModifierEvent>(OnGetPryMod);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorPryEvent>(OnDoorPry);

            SubscribeLocalEvent<AirlockComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<AirlockComponent, EntInsertedIntoContainerMessage>(OnInserted);
            SubscribeLocalEvent<AirlockComponent, EntRemovedFromContainerMessage>(OnRemoved);
        }

        private void OnMapInit(EntityUid uid, AirlockComponent door, MapInitEvent args)
        {
            // This Timer is here for the same reason that it exists on DoorSystem.
            // See the comments there for an explanation.
            Timer.Spawn(1, () =>
            {
                if (Deleted(uid))
                    return;

                var paintContainer = _containerSystem.EnsureContainer<Container>(uid, "paint");

                if (TryComp(uid, out ConstructionComponent? construction))
                    _constructionSystem.AddContainer(uid, "paint", construction);

                if (paintContainer.ContainedEntities.Count != 0)
                    return;

                // I'm using a container to transfer state across the construction graph.
                // I have no idea how else to accomplish this.
                //
                // If this becomes more common, it might be better to rework Construction
                // to support arbitrary metadata which is transferred between entities,
                // or change IGraphAction to support accessing the old Entity.

                var paint = EntityManager.SpawnEntity("AirlockAssemblyPaint", Transform(uid).Coordinates);

                // DoorVisuals.BaseRSI is only set when an airlock is painted,
                // so we need the SpriteComponent's RSI.
                if (EntityManager.TryGetComponent<SpriteComponent>(uid, out var doorSpriteComponent)
                    && EntityManager.TryGetComponent<SpriteComponent>(paint, out var paintSpriteComponent)
                    && doorSpriteComponent.BaseRSIPath != null)
                {
                    paintSpriteComponent.BaseRSIPath = doorSpriteComponent.BaseRSIPath;
                }

                if(!paintContainer.Insert(paint))
                    Logger.Warning($"Couldn't insert paint {ToPrettyString(paint)} into door {ToPrettyString(uid)}!");
            });
        }

        private void OnInserted(EntityUid uid, AirlockComponent component, ContainerModifiedMessage args)
        {
            if (args.Container.ID == "paint"
                && TryComp<SpriteComponent>(args.Entity, out var spriteComponent)
                && spriteComponent.BaseRSIPath != null)
            {
                _appearance.SetData(uid, DoorVisuals.BaseRSI, spriteComponent.BaseRSIPath);
            }
        }

        private void OnRemoved(EntityUid uid, AirlockComponent component, ContainerModifiedMessage args)
        {
            if (args.Container.ID == "paint"
                && TryComp<SpriteComponent>(args.Entity, out var paintSpriteComponent))
            {
                // First check if the door has been painted.
                // If it has, there will be a new RSI in the Appearance data.
                if (_appearance.TryGetData(uid, DoorVisuals.BaseRSI, out var base_rsi)
                    && base_rsi != null)
                {
                    paintSpriteComponent.BaseRSIPath = (string) base_rsi;
                }
                // Otherwise, just get the default sprite for the door.
                else if (TryComp<SpriteComponent>(uid, out var airlockSpriteComponent)
                    && airlockSpriteComponent.BaseRSIPath != null)
                {
                    paintSpriteComponent.BaseRSIPath = airlockSpriteComponent.BaseRSIPath;
                }
            }
        }

        private void OnPowerChanged(EntityUid uid, AirlockComponent component, PowerChangedEvent args)
        {
            if (TryComp<AppearanceComponent>(uid, out var appearanceComponent))
            {
                appearanceComponent.SetData(DoorVisuals.Powered, args.Powered);
            }

            if (!TryComp(uid, out DoorComponent? door))
                return;

            if (!args.Powered)
            {
                // stop any scheduled auto-closing
                if (door.State == DoorState.Open)
                    DoorSystem.SetNextStateChange(uid, null);
            }
            else
            {
                UpdateAutoClose(uid, door: door);
            }

            // BoltLights also got out
            component.UpdateBoltLightStatus();
        }

        private void OnStateChanged(EntityUid uid, AirlockComponent component, DoorStateChangedEvent args)
        {
            // TODO move to shared? having this be server-side, but having client-side door opening/closing & prediction
            // means that sometimes the panels & bolt lights may be visible despite a door being completely open.

            // Only show the maintenance panel if the airlock is closed
            if (TryComp<WiresComponent>(uid, out var wiresComponent))
            {
                wiresComponent.IsPanelVisible =
                    component.OpenPanelVisible
                    ||  args.State != DoorState.Open;
            }
            // If the door is closed, we should look if the bolt was locked while closing
            component.UpdateBoltLightStatus();

            UpdateAutoClose(uid, component);

            // Make sure the airlock auto closes again next time it is opened
            if (args.State == DoorState.Closed)
                component.AutoClose = true;
        }

        /// <summary>
        /// Updates the auto close timer.
        /// </summary>
        public void UpdateAutoClose(EntityUid uid, AirlockComponent? airlock = null, DoorComponent? door = null)
        {
            if (!Resolve(uid, ref airlock, ref door))
                return;

            if (door.State != DoorState.Open)
                return;

            if (!airlock.AutoClose)
                return;

            if (!airlock.CanChangeState())
                return;

            var autoev = new BeforeDoorAutoCloseEvent();
            RaiseLocalEvent(uid, autoev, false);
            if (autoev.Cancelled)
                return;

            DoorSystem.SetNextStateChange(uid, airlock.AutoCloseDelay * airlock.AutoCloseDelayModifier);
        }

        private void OnBeforeDoorOpened(EntityUid uid, AirlockComponent component, BeforeDoorOpenedEvent args)
        {
            if (!component.CanChangeState())
                args.Cancel();
        }

        protected override void OnBeforeDoorClosed(EntityUid uid, SharedAirlockComponent component, BeforeDoorClosedEvent args)
        {
            base.OnBeforeDoorClosed(uid, component, args);

            if (args.Cancelled)
                return;

            // only block based on bolts / power status when initially closing the door, not when its already
            // mid-transition. Particularly relevant for when the door was pried-closed with a crowbar, which bypasses
            // the initial power-check.

            if (TryComp(uid, out DoorComponent? door)
                && !door.Partial
                && !Comp<AirlockComponent>(uid).CanChangeState())
            {
                args.Cancel();
            }
        }

        private void OnBeforeDoorDenied(EntityUid uid, AirlockComponent component, BeforeDoorDeniedEvent args)
        {
            if (!component.CanChangeState())
                args.Cancel();
        }

        private void OnActivate(EntityUid uid, AirlockComponent component, ActivateInWorldEvent args)
        {
            if (TryComp<WiresComponent>(uid, out var wiresComponent) && wiresComponent.IsPanelOpen &&
                EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
            {
                _wiresSystem.OpenUserInterface(uid, actor.PlayerSession);
                args.Handled = true;
                return;
            }

            if (component.KeepOpenIfClicked)
            {
                // Disable auto close
                component.AutoClose = false;
            }
        }

        private void OnGetPryMod(EntityUid uid, AirlockComponent component, DoorGetPryTimeModifierEvent args)
        {
            if (_power.IsPowered(uid))
                args.PryTimeModifier *= component.PoweredPryModifier;
        }

        private void OnDoorPry(EntityUid uid, AirlockComponent component, BeforeDoorPryEvent args)
        {
            if (component.IsBolted())
            {
                component.Owner.PopupMessage(args.User, Loc.GetString("airlock-component-cannot-pry-is-bolted-message"));
                args.Cancel();
            }
            if (component.IsPowered())
            {
                if (HasComp<ToolForcePoweredComponent>(args.Tool))
                    return;
                component.Owner.PopupMessage(args.User, Loc.GetString("airlock-component-cannot-pry-is-powered-message"));
                args.Cancel();
            }
        }
    }
}
