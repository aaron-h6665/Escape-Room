using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class DataPersistenceManager : MonoBehaviour
{
    [Header("File Storage Config")]
    [SerializeField] private string fileName = SaveFileUtility.DefaultSaveFileName;

    private GameData gameData;
    private List<IDataPersistence> dataPersistenceObjects;
    private FileDataHandler dataHandler;
    public static DataPersistenceManager instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one Data Persistence Manager in the scence.");
        }
        instance = this;
    }

    private void Start()
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = SaveFileUtility.DefaultSaveFileName;
        }

        this.dataHandler = new FileDataHandler(Application.persistentDataPath, fileName);
        this.dataPersistenceObjects = FindAllDataPersistenceObjects();

        if (ReplayManager.IsPlaybackLaunch())
        {
            NewGame();
            Debug.Log("Replay playback launch detected. Skipping data.game load.");
            return;
        }

        if (ReplayManager.IsRecordingLaunch())
        {
            NewGame();
            Debug.Log("Replay recording launch detected. Starting from new GameData.");
            return;
        }

        LoadGame();
    }

    public void NewGame()
    {
        this.gameData = new GameData();
    }

    public void LoadGame()
    {
        this.gameData = dataHandler.Load();

        if (this.gameData == null)
        {
            NewGame();
            Debug.Log("No save data was found. Starting a new game.");
            return;
        }

        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
        {
            dataPersistenceObj.LoadData(gameData);
        }

        Debug.Log("Time Elapsed = " + gameData.elapsedTime);
    }

    public void SaveGame()
    {
        if (ReplayManager.IsPlaybackLaunch() || ReplayManager.IsRecordingLaunch())
        {
            Debug.Log("Game save skipped during replay test mode.");
            return;
        }

        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
        {
            dataPersistenceObj.SaveData(ref gameData);
        }

        Debug.Log("Time Elapsed = " + gameData.elapsedTime);

        dataHandler.Save(gameData);
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private List<IDataPersistence> FindAllDataPersistenceObjects()
    {
        IEnumerable<IDataPersistence> dataPersistenceObjects = FindObjectsOfType<MonoBehaviour>().OfType<IDataPersistence>();

        return new List<IDataPersistence>(dataPersistenceObjects);
    }
}
