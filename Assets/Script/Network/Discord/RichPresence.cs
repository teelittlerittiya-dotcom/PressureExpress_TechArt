using UnityEngine;
using Discord.Sdk;

public class RichPresence : MonoBehaviour
{
    [SerializeField]
    private string details = "In Unity";

    [SerializeField]
    private string state = "Building a game";

    private ulong startTimestamp;

    void Start()
    {
        startTimestamp = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void UpdateRichPresence(Client client)
    {
        Activity activity = new Activity();

        activity.SetType(ActivityTypes.Playing);
        activity.SetDetails(details);
        activity.SetState(state);
        var joinButton = new ActivityButton();
        joinButton.SetLabel("Join I Non");
        joinButton.SetUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        activity.AddButton(joinButton);

        var activityTimestamp = new ActivityTimestamps();
        activityTimestamp.SetStart(startTimestamp);
        activity.SetTimestamps(activityTimestamp);

        var activityParty = new ActivityParty();
        activityParty.SetId("party1234");
        activityParty.SetCurrentSize(1);
        activityParty.SetMaxSize(4);
        activity.SetParty(activityParty);

        client.UpdateRichPresence(activity, OnUpdateRichPresence);
    }
    private void OnUpdateRichPresence(ClientResult result)
    {
        if (result.Successful())
        {
            Debug.Log("Rich presence updated!");
        }
        else
        {
            Debug.LogError($"Failed to update rich presence {result.Error()}");
        }
    }
    
    #region Update Prestntence Methods
    public void UpdateState(string newState)
    {
        state = newState;
    }
    public void UpdateDetail(string newDetail)
    {
        details = newDetail;
    }
    #endregion
}