using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using PressureExpress.Framework;

public class AnalyticManager : MonoBehaviour
{
    public Dictionary<string, float> roomDuration = new Dictionary<string, float>();
    public Dictionary<string, float> MachineDuration = new Dictionary<string, float>();
    public Dictionary<string, int> selecedNode = new Dictionary<string, int>();

    private static AnalyticManager _instance;
    public static AnalyticManager Instance => _instance ?? ServiceLocator.Get<AnalyticManager>();
    public static AnalyticManager instance => Instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            ServiceLocator.Register(this);
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            ServiceLocator.Unregister<AnalyticManager>(this);
            _instance = null;
        }
    }

    private void Start()
    {
        InitAsync().Forget();
    }

    public bool IsReady { get; private set; }

    public async UniTask InitAsync()
    {
        try
        {
            // Routed through the shared bootstrap: VivoxManager also needs UnityServices, and two
            // concurrent UnityServices.InitializeAsync calls is a known source of flaky failures.
            if (!await UnityServicesBootstrap.EnsureInitializedAsync()) return;

            AnalyticsService.Instance.StartDataCollection();
            IsReady = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Analytics Initialization Failed: {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        SendAllData();
    }

    public void SendAllData()
    {
        // Recording against an uninitialised AnalyticsService throws, and this runs from
        // OnApplicationQuit where an exception is easy to miss.
        if (!IsReady) return;

        foreach (var item in roomDuration)
        {
            CustomEvent room = new CustomEvent("RoomDuration")
            {
                {"RoomName", item.Key },
                {"inRoomDurationPerRound" , item.Value}
            };
            AnalyticsService.Instance.RecordEvent(room);
        }

        foreach (var item in MachineDuration)
        {
            CustomEvent machine = new CustomEvent("MachineDuration")
            {
                {"machineName", item.Key },
                {"machineDuration", item.Value }
            };
            AnalyticsService.Instance.RecordEvent(machine);
        }

        foreach (var item in selecedNode)
        {
            CustomEvent selecNode = new CustomEvent("NodePlayerSelect")
            {
                {"nodeName", item.Key },
                {"nodeCount", item.Value }
            };
            AnalyticsService.Instance.RecordEvent(selecNode);
        }
    }

    public float GetTimeDuration(float startTime)
    {
        return Time.time - startTime;
    }

    public void UpdateRoom(string name, float duration)
    {
        if (roomDuration.ContainsKey(name))
        {
            roomDuration[name] += duration;
        }
        else
        {
            roomDuration.Add(name, duration);
        }
    }

    public void UpdateMachine(string name, float duration)
    {
        if (MachineDuration.ContainsKey(name))
        {
            MachineDuration[name] += duration;
        }
        else
        {
            MachineDuration.Add(name, duration);
        }
    }

    public void UpdateNode(string name)
    {
        if (selecedNode.ContainsKey(name))
        {
            selecedNode[name]++;
        }
        else
        {
            selecedNode.Add(name, 1);
        }
    }
}
