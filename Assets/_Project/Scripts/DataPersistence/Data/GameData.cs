using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class GameData
{

    public float elapsedTime;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public Dictionary<string, bool> interacted;

    public GameData()
    {
        this.elapsedTime = 0f;
        playerPosition = Vector3.zero;
        playerRotation = Quaternion.identity;
        interacted = new Dictionary<string, bool>();
    }
}
