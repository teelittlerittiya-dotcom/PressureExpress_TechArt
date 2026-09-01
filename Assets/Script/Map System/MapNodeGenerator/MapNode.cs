using System;
using System.Collections.Generic;
using Unity.Netcode;

public class MapNode<T> 
{
    public T Data { get; set; }

    public MapNode<T>? Parent { get; set; }

    public List<MapNode<T>> Children { get; set; }

    public MapNode(T data)
    {
        Data = data;
        Children = new List<MapNode<T>>();
    }

    public MapNode<T> AddChild(T childData)
    {
        var child = new MapNode<T>(childData)
        {
            Parent = this
        };

        Children.Add(child);
        return child;
    }
    public int GetChildCount() 
    {
        return Children.Count; 
    }
}
