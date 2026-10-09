using System;
using System.Linq;
using System.Reflection;

namespace Godot;

public partial class ComponentApplier : Node
{
	public override void _EnterTree()
	{
		var editorComponents = ResourceLoader.Load<EditorComponents>(MonoComponents.ComponentsResourcePath, cacheMode: ResourceLoader.CacheMode.Ignore);

		GetTree().NodeAdded += (node) =>
		{
			var sceneRoot = node.GetTree().CurrentScene;
			foreach (var assignment in editorComponents.GetAssignments(sceneRoot.SceneFilePath, sceneRoot.GetPathTo(node)))
			{
				var type = Assembly.GetExecutingAssembly().GetType(assignment.ComponentName);
				if (type is null)
				{
					GD.PushError($"Couldn't find a component named '{assignment.ComponentName}' for {node.GetPath()}!");
					continue;
				}

				var addComponentMethod = typeof(ComponentInjection)
					.GetMethods(BindingFlags.Static | BindingFlags.Public)
					.First(m => m is { Name: "AddComponent", IsGenericMethod: true });

				var genericMethod = addComponentMethod.MakeGenericMethod(type);

				var component = (ComponentNode?)genericMethod.Invoke(null, [node]);
				if (component == null) continue;

				foreach (var (propName, value) in assignment.Exports)
				{
					var prop = type.GetProperty(propName);
					if (prop == null) continue;

					if (prop.PropertyType.IsSubclassOf(typeof(Enum)))
						prop.SetValue(component, Enum.ToObject(prop.PropertyType, value.Obj!));
					else if ((prop.PropertyType == typeof(float)) && (value.VariantType == Variant.Type.Float))
						prop.SetValue(component, value.AsSingle());
					else
						prop.SetValue(component, value.Obj);
				}
			}
		};
	}
}
