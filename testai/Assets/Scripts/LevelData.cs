using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "Puzzle/LevelData")]
public class LevelData : ScriptableObject
{
    public enum Side { Left, Right, Top, Bottom }

    [System.Serializable]
    public struct ExitGate
    {
        public int rowOrColumn; 
        public Side side;
    }

    [System.Serializable]
    public struct CarInfo
    {
        public Vector2Int position;
        public CarController.Orientation orientation;
        public int size;
        public bool isTargetCar;
        public int targetGateIndex; 
    }

    public int width = 6;
    public int height = 6;
    public List<ExitGate> exitGates;
    public List<CarInfo> cars;
    }
