using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;

namespace CznCore.Godot;

public static class GodotNodeUtil
{
	public static bool IsAlive([NotNullWhen(true)] GodotObject? obj)
	{
		if (obj != null)
		{
			return GodotObject.IsInstanceValid(obj);
		}
		return false;
	}

	public static bool IsClass([NotNullWhen(true)] GodotObject? obj, string className)
	{
		if (IsAlive(obj))
		{
			return string.Equals(obj.GetClass(), className, StringComparison.Ordinal);
		}
		return false;
	}

	public static IEnumerable<Node> TraverseDepthFirst(Node root)
	{
		Stack<Node> stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			yield return node;
			foreach (Node child in node.GetChildren())
			{
				stack.Push(child);
			}
		}
	}

	public static List<Node> FindNodesByClass(Node root, string className)
	{
		List<Node> list = new List<Node>();
		foreach (Node item in TraverseDepthFirst(root))
		{
			if (IsClass(item, className))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static void SafeQueueFree(Node? node)
	{
		if (IsAlive(node))
		{
			node.QueueFree();
		}
	}

	public static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}
}
