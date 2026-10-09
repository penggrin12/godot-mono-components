using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Godot;

#nullable enable
public static class ComponentInjection
{
	private static readonly ConditionalWeakTable<Node, Dictionary<Type, ComponentNode>> Components = [];
	
	public static bool TryAddComponent<T>(this Node node, [NotNullWhen(true)] out T? component) where T : ComponentNode, new()
	{
		component = new T() { Host = node };
		node.AddChild(component);
		return Components.GetOrCreateValue(node).TryAdd(typeof(T), component);
	}

	public static T AddComponent<T>(this Node node) where T : ComponentNode, new()
	{
		return !node.TryAddComponent<T>(out var component) ?
			throw new InvalidOperationException($"Component of type '{typeof(T).FullName}' already exists on the node.") : component;
	}
	
	public static bool TryGetComponent<T>(this Node node, [NotNullWhen(true)] out T? component) where T : ComponentNode
	{
		component = null;
		if (!Components.GetOrCreateValue(node).TryGetValue(typeof(T), out var comp))
			return false;
		component = comp as T;
		return component != null;
	}

	public static T GetComponent<T>(this Node node) where T : ComponentNode
	{
		return !node.TryGetComponent(out T? component) ?
			throw new KeyNotFoundException($"Component of type '{typeof(T).FullName}' was not found on the node.") : component;
	}

	public static bool TryRemoveComponent<T>(this Node node) where T : ComponentNode
	{
		if (!Components.GetOrCreateValue(node).Remove(typeof(T), out var comp))
			return false;
		comp.QueueFree();
		return true;
	}

	public static void RemoveComponent<T>(this Node node) where T : ComponentNode
	{
		if (!node.TryRemoveComponent<T>())
			throw new KeyNotFoundException($"Component of type '{typeof(T).FullName}' was not found on the node.");
	}

	public static bool HasComponent<T>(this Node node) where T : ComponentNode
		=> Components.GetOrCreateValue(node).ContainsKey(typeof(T));
}
#nullable restore