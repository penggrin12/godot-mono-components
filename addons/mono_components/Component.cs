using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Godot;

public abstract partial class ComponentNode : Node
{
	public Node Host { get; init; } = null!;
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetSibling<TComponent>([NotNullWhen(true)] out TComponent? otherComponent) where TComponent : ComponentNode
		=> Host.TryGetComponent(out otherComponent);
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TComponent GetSibling<TComponent>() where TComponent : ComponentNode
		=> Host.GetComponent<TComponent>();
}

public abstract partial class Component<THost> : ComponentNode
	where THost : Node
{
	public new THost Host
	{
		get => (THost)base.Host;
		init => base.Host = value;
	}
}
