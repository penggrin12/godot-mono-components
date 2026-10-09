using Godot.Collections;

namespace Godot;

[GlobalClass, Tool]
public partial class EditorComponents : Resource
{
    // (scene path : (node path : (list of component class names)))
    [Export]
    public Dictionary<string, Dictionary<NodePath, Array<EditorComponentAssignment>>> Assignments { get; set; } = new();

    public void AddAssignment(string scenePath, NodePath nodePath, EditorComponentAssignment componentAssignment)
    {
        if (!Assignments.ContainsKey(scenePath))
            Assignments.Add(scenePath, new());
        if (!Assignments[scenePath].ContainsKey(nodePath))
            Assignments[scenePath].Add(nodePath, new());

        if (Assignments[scenePath][nodePath].Contains(componentAssignment))
            return;

        Assignments[scenePath][nodePath].Add(componentAssignment);
    }

    public void RemoveAssignment(string scenePath, NodePath nodePath, EditorComponentAssignment componentAssignment)
    {
        if (!Assignments.ContainsKey(scenePath))
            return;
        if (!Assignments[scenePath].ContainsKey(nodePath))
            return;

        if (!Assignments[scenePath][nodePath].Remove(componentAssignment))
            return;

        // clean up if empty
        if (Assignments[scenePath][nodePath].Count == 0)
            Assignments[scenePath].Remove(nodePath);
        if (Assignments[scenePath].Count == 0)
            Assignments.Remove(scenePath);
    }

    public Array<EditorComponentAssignment> GetAssignments(string scenePath, NodePath nodePath)
    {
        if (Assignments.TryGetValue(scenePath, out var nodeDict)
            && nodeDict.TryGetValue(nodePath, out var components))
            return components;

        return [];
    }
}