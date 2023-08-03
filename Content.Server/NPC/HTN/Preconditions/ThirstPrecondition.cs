using Content.Server.Nutrition.EntitySystems;
using Content.Server.Nutrition.Components;

namespace Content.Server.NPC.HTN.Preconditions;

/// <summary>
/// Checks the state of the owner's thirst.
/// </summary>
public sealed class ThirstPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ThirstSystem _thirst = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("threshold")]
    public ThirstThreshold Threshold = ThirstThreshold.Thirsty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _thirst = sysManager.GetEntitySystem<ThirstSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<ThirstComponent>(owner, out var thirst))
            return false;

        return _thirst.GetThirstThreshold(thirst) <= Threshold;
    }
}
