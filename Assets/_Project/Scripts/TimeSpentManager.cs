using UnityEngine;
using TMPro;

public class TimeSpentManager : MonoBehaviour, IDataPersistence
{
    [SerializeField] private TMP_Text timeSpentText;
    private float elapsedTime = 0f;
    private bool isRunning = true;

    public void LoadData(GameData data)
    {
        this.elapsedTime = data.elapsedTime;
    }

    public void SaveData(ref GameData data)
    {
        data.elapsedTime = this.elapsedTime;
    }

    void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;

        int minutes = Mathf.FloorToInt(elapsedTime / 60);
        int seconds = Mathf.FloorToInt(elapsedTime % 60);
        
        timeSpentText.SetText("Time Spent: " + $"{minutes:00}:{seconds:00}");
    }

    public void ToggleTimer() => isRunning = !isRunning;
    public void ResetTimer() => elapsedTime = 0f;
}
