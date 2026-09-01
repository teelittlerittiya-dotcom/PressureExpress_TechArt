using UnityEngine;
using System.Collections.Generic;

public class RadarWaypoint : MonoBehaviour
{
    public static readonly List<RadarWaypoint> ActiveWaypoints = new List<RadarWaypoint>();

    [SerializeField] private string defaultWaypointName = "EXIT BEACON";
    private string customWaypointName;

    public string WaypointName
    {
        get => !string.IsNullOrEmpty(customWaypointName) ? customWaypointName : (!string.IsNullOrEmpty(defaultWaypointName) ? defaultWaypointName : gameObject.name);
        private set => customWaypointName = value;
    }

    public void Setup(string name)
    {
        customWaypointName = name;
    }

    private void OnEnable()
    {
        if (!ActiveWaypoints.Contains(this))
            ActiveWaypoints.Add(this);
    }

    private void OnDisable()
    {
        if (ActiveWaypoints.Contains(this))
            ActiveWaypoints.Remove(this);
    }
}