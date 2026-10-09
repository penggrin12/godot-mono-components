using Godot.Collections;

namespace Godot;

[GlobalClass, Tool]
public partial class EditorComponentAssignment : Resource
{
	[Export] public string ComponentName { get; set; } = null!;
	[Export] public Dictionary<string, Variant> Exports { get; set; } = [];
} 