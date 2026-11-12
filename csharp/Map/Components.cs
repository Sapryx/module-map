namespace ModuleMap.Map;

public sealed class Components
{
    private readonly Func<ModuleDraft, IEnumerable<ModuleDraft>> next;
    private readonly Dictionary<ModuleDraft, int> indexes = new Dictionary<ModuleDraft, int>();
    private readonly Dictionary<ModuleDraft, int> lowLinks = new Dictionary<ModuleDraft, int>();
    private readonly Stack<ModuleDraft> stack = new Stack<ModuleDraft>();
    private readonly HashSet<ModuleDraft> onStack = [];
    private readonly List<List<ModuleDraft>> components = [];

    private Components(Func<ModuleDraft, IEnumerable<ModuleDraft>> next)
    {
        this.next = next;
    }

    public static List<List<ModuleDraft>> Find(
        IEnumerable<ModuleDraft> nodes,
        Func<ModuleDraft, IEnumerable<ModuleDraft>> next
    )
    {
        var search = new Components(next);
        foreach(var node in nodes)
        {
            if(!search.indexes.ContainsKey(node))
            {
                search.Visit(node);
            }
        }

        return search.components;
    }

    private void Visit(ModuleDraft node)
    {
        indexes[node] = indexes.Count;
        lowLinks[node] = indexes[node];
        stack.Push(node);
        onStack.Add(node);
        foreach(var target in next(node))
        {
            if(!indexes.ContainsKey(target))
            {
                Visit(target);
                lowLinks[node] = Math.Min(lowLinks[node], lowLinks[target]);
            }
            else if(onStack.Contains(target))
            {
                lowLinks[node] = Math.Min(lowLinks[node], indexes[target]);
            }
        }

        if(lowLinks[node] != indexes[node])
        {
            return;
        }

        var component = new List<ModuleDraft>();
        ModuleDraft member;
        do
        {
            member = stack.Pop();
            onStack.Remove(member);
            component.Add(member);
        }
        while(member != node);
        components.Add(component);
    }
}
