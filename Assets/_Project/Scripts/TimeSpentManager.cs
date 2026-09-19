using UnityEngine;
using TMPro;

public class TimeSpentManager : MonoBehaviour, IDataPersistence, IReplayObject
{
    [SerializeField] private TMP_Text timeSpentText;
    private float elapsedTime = 0f;
    private bool isRunning = true;
    public float ElapsedTime => elapsedTime;

    void Start()
    {
        if (ReplayManager.instance != null)
        {
            ReplayManager.instance.Register(this);
        }
    }

    public void LoadData(GameData data)
    {
        this.elapsedTime = data.elapsedTime;
        UpdateTimeText();
    }

    public void SaveData(ref GameData data)
    {
        data.elapsedTime = this.elapsedTime;
    }

    public void SaveSnapshot(ref GameData data)
    {
        data.elapsedTime = elapsedTime;
    }

    public void LoadSnapshot(GameData data)
    {
        elapsedTime = data.elapsedTime;
        UpdateTimeText();
    }

    public void ApplyReplayElapsedTime(float value)
    {
        elapsedTime = value;
        UpdateTimeText();
    }

    void Update()
    {
        if (ReplayManager.instance != null && ReplayManager.instance.CurrentState != ReplayManager.State.Idle)
        {
            return;
        }

        if (!isRunning || ReplayManager.instance != null) return;

        elapsedTime += Time.deltaTime;
        UpdateTimeText();
    }

    void UpdateTimeText()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60);
        int seconds = Mathf.FloorToInt(elapsedTime % 60);

        if (timeSpentText != null)
        {
            timeSpentText.SetText("Time Spent: " + $"{minutes:00}:{seconds:00}");
        }
    }

    public void ToggleTimer() => isRunning = !isRunning;
    public void ResetTimer() => elapsedTime = 0f;
}
