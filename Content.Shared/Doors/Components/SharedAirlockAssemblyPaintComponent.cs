using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Doors.Components;

[RegisterComponent, NetworkedComponent]
public sealed class SharedAirlockAssemblyPaintComponent : Component
{
    private string? _style;

    /// <summary>
    /// The paint style of an AirlockGroup that this refers to.
    /// </summary>
    [ViewVariables]
    public string? Style
    {
        get => _style;
        set
        {
            _style = value;
            Dirty();
        }
    }

    [Serializable, NetSerializable]
    public sealed class AirlockAssemblyPaintComponentState : ComponentState
    {
        public string? Style { get; }

        public AirlockAssemblyPaintComponentState(SharedAirlockAssemblyPaintComponent paint)
        {
            Style = paint.Style;
        }
    }
}

