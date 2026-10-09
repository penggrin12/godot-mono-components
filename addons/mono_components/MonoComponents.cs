namespace Godot;

[Tool]
public partial class MonoComponents : EditorPlugin
{
	public const string ComponentsResourcePath = "uid://bhmu5728pnw6o";

	private MonoComponentsInspector? _inspectorPlugin;

	public override void _EnablePlugin()
	{
		AddAutoloadSingleton("MonoComponentApplier", "uid://cl622uwg01f6o");
	}

	public override void _DisablePlugin()
	{
		RemoveAutoloadSingleton("MonoComponentApplier");
	}

	public override void _EnterTree()
	{
		if (Engine.IsEditorHint())
		{
			if (!FileAccess.FileExists(ComponentsResourcePath))
				ResourceSaver.Save(new EditorComponents(), ComponentsResourcePath);

#if TOOLS
			_inspectorPlugin = new();
			AddInspectorPlugin(_inspectorPlugin);
#endif
		}
	}

	public override void _ExitTree()
	{
		if (Engine.IsEditorHint())
		{
#if TOOLS
			RemoveInspectorPlugin(_inspectorPlugin!);
#endif
		}
	}
}
