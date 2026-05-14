using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public int width = 6;
    public int height = 6;
    public Vector3 gridOffset = Vector3.zero;
    public List<LevelData> levels;
    private int currentLevelIndex = 0;

    public List<GameObject> obstaclePrefabs;
    public GameObject targetCarPrefab;
    public List<GameObject> truckPrefabs;
    public GameObject curbPrefab;
    public GameObject exitGatePrefab;
    public GameObject winEffectPrefab;
    public AudioClip winSound;
    private AudioSource audioSource;

    private List<GameObject> spawnedGates = new List<GameObject>();
    private List<GameObject> spawnedCurbs = new List<GameObject>();
    private List<GameObject> spawnedCars = new List<GameObject>();
    private CarController[,] grid;
    private int targetsExited = 0;
    private int requiredExits = 0;
    private bool isLevelComplete = false;
    private bool hasTriggeredWin = false;
    private const string LEVEL_KEY = "CurrentLevelIndex";

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        grid = new CarController[width, height];
        currentLevelIndex = PlayerPrefs.GetInt(LEVEL_KEY, 0);
        if (levels != null && levels.Count > 0)
        {
            if (currentLevelIndex >= levels.Count) currentLevelIndex = 0;
            LoadLevel(levels[currentLevelIndex]);
        }
    }

    [ContextMenu("Load Level")]
    public void LoadLevelEditor()
    {
        if (levels != null && levels.Count > 0) LoadLevel(levels[currentLevelIndex]);
    }

    public int GetCurrentLevelIndex() => currentLevelIndex;

    public void NextLevel()
    {
        currentLevelIndex = (currentLevelIndex + 1) % levels.Count;
        PlayerPrefs.SetInt(LEVEL_KEY, currentLevelIndex);
        PlayerPrefs.Save();
        LoadLevelByIndex(currentLevelIndex);
    }

    public void RestartLevel() => LoadLevelByIndex(currentLevelIndex);

    public void LoadLevelByIndex(int index)
    {
        if (index < 0 || index >= levels.Count) return;
        currentLevelIndex = index;
        LoadLevel(levels[currentLevelIndex]);
        UIManager ui = Object.FindAnyObjectByType<UIManager>();
        if (ui != null) ui.UpdateLevelUI();
    }

    public void LoadLevel(LevelData level)
    {
        width = level.width;
        height = level.height;
        grid = new CarController[width, height];

        Cleanup();
        targetsExited = 0;
        requiredExits = 0;
        isLevelComplete = false;
        hasTriggeredWin = false;

        AdjustCamera(width, height);
        UpdateFloor(width, height);
        SpawnPerimeter(level);

        foreach (var info in level.cars)
        {
            GameObject prefab = info.isTargetCar ? targetCarPrefab : GetObstaclePrefab(info);
            if (prefab == null) continue;

            GameObject carObj = Instantiate(prefab, new Vector3(info.position.x + gridOffset.x, 0, info.position.y + gridOffset.z), Quaternion.identity);
            CarController controller = carObj.GetComponent<CarController>();
            controller.orientation = info.orientation;
            controller.size = info.size;
            controller.isTargetCar = info.isTargetCar;

            if (info.isTargetCar) requiredExits++;

            Transform pivot = carObj.transform.Find("ModelPivot");
            BoxCollider box = carObj.GetComponent<BoxCollider>();

            float margin = 0.15f;
            float targetL = info.size - (margin * 2.0f);

            if (pivot != null)
            {
                float scaleZ = info.size == 3 ? targetL - 0.3f : targetL;
                pivot.localScale = new Vector3(targetL, targetL, scaleZ);
            
                if (info.orientation == CarController.Orientation.Horizontal)
                {
                    pivot.localPosition = new Vector3(margin, 0, 0.5f);
                    pivot.localRotation = Quaternion.identity;
                }
                else
                {
                    pivot.localPosition = new Vector3(0.5f, 0, margin);
                    pivot.localRotation = Quaternion.Euler(0, 270, 0);
                }
            }

            if (box != null)
            {
                if (info.orientation == CarController.Orientation.Horizontal)
                {
                    box.center = new Vector3(info.size * 0.5f, 0.25f, 0.5f);
                    box.size = new Vector3(info.size, 0.5f, 1.0f);
                }
                else
                {
                    box.center = new Vector3(0.5f, 0.25f, info.size * 0.5f);
                    box.size = new Vector3(1.0f, 0.5f, info.size);
                }
            }
            spawnedCars.Add(carObj);
        }
        UpdateGrid();
    }

    private void Cleanup()
    {
        if (spawnedCars != null) {
            foreach (var go in spawnedCars) if (go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            spawnedCars.Clear();
        }
        if (spawnedGates != null) {
            foreach (var go in spawnedGates) if (go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            spawnedGates.Clear();
        }
        if (spawnedCurbs != null) {
            foreach (var go in spawnedCurbs) if (go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            spawnedCurbs.Clear();
        }

        CarController[] legacyCars = Object.FindObjectsByType<CarController>(FindObjectsInactive.Include);
        foreach (var c in legacyCars) if (c && c.gameObject) { if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject); }
        
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allObjects) {
            if (go == null) continue;
            string n = go.name;
            if (n.Contains("Curb") || n.Contains("ExitGate") || n.Contains("ModelPivot") || n.Contains("NewCar") || 
                n.Contains("VLine") || n.Contains("HLine") || n.Contains("Num_") || n.Contains("Barrier")) {
                if (go.name == "CurbContainer" || go.name == "Barrier") continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }
    }

    private void UpdateFloor(int w, int h)
    {
        GameObject floor = GameObject.Find("FloorCube");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "FloorCube";
        }
        
        // Floor surface at Y=0, slightly larger to accommodate camera margins
        floor.transform.position = new Vector3(w / 2.0f, -0.1f, h / 2.0f);
        floor.transform.localScale = new Vector3(w + 5.0f, 0.2f, h + 5.0f);
        
        Renderer r = floor.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial.color = new Color(0.12f, 0.12f, 0.14f);
        }
    }

    private void SpawnPerimeter(LevelData level)
    {
        if (level.exitGates != null && level.exitGates.Count > 0)
        {
            foreach (var g in level.exitGates) CreateExitGate(g);
        }
        else
        {
            CreateExitGate(new LevelData.ExitGate { side = LevelData.Side.Right, rowOrColumn = 2 });
        }

        if (curbPrefab == null) return;
        
        float thickness = 0.25f;
        float hScale = 0.5f; 
        float outerOffset = thickness * 0.5f;

        // Horizontal: X=-90, Z=90
        for (int x = 0; x < width; x++) {
            if (!HasGate(LevelData.Side.Bottom, x)) SpawnCurb(new Vector3(x + gridOffset.x + 0.5f, 0, -outerOffset + gridOffset.z), new Vector3(-90, 0, 90), thickness, hScale, 1.0f);
            if (!HasGate(LevelData.Side.Top, x)) SpawnCurb(new Vector3(x + gridOffset.x + 0.5f, 0, level.height + outerOffset + gridOffset.z), new Vector3(-90, 0, 90), thickness, hScale, 1.0f);
        }
        // Vertical: X=-90, Z=0 (Rotate Horizontal by 90 on Y)
        for (int z = 0; z < height; z++) {
            if (!HasGate(LevelData.Side.Left, z)) SpawnCurb(new Vector3(-outerOffset + gridOffset.x, 0, z + gridOffset.z + 0.5f), new Vector3(-90, 0, 0), thickness, hScale, 1.0f);
            if (!HasGate(LevelData.Side.Right, z)) SpawnCurb(new Vector3(width + outerOffset + gridOffset.x, 0, z + gridOffset.z + 0.5f), new Vector3(-90, 0, 0), thickness, hScale, 1.0f);
        }
        
        // Corners
        SpawnCurb(new Vector3(-outerOffset + gridOffset.x, 0, -outerOffset + gridOffset.z), new Vector3(-90, 0, 90), thickness, hScale, thickness);
        SpawnCurb(new Vector3(width + outerOffset + gridOffset.x, 0, -outerOffset + gridOffset.z), new Vector3(-90, 0, 90), thickness, hScale, thickness);
        SpawnCurb(new Vector3(-outerOffset + gridOffset.x, 0, level.height + outerOffset + gridOffset.z), new Vector3(-90, 0, 90), thickness, hScale, thickness);
        SpawnCurb(new Vector3(width + outerOffset + gridOffset.x, 0, level.height + outerOffset + gridOffset.z), new Vector3(-90, 0, 90), thickness, hScale, thickness);
    }

    private bool HasGate(LevelData.Side side, int idx) {
        LevelData level = levels[currentLevelIndex];
        if (level.exitGates == null || level.exitGates.Count == 0) return side == LevelData.Side.Right && idx == 2;
        foreach (var g in level.exitGates) if (g.side == side && g.rowOrColumn == idx) return true;
        return false;
    }

    private void SpawnCurb(Vector3 pos, Vector3 euler, float thickness, float hScale, float length) {
        GameObject c = Instantiate(curbPrefab, pos, Quaternion.identity);
        c.transform.localEulerAngles = euler;
        
        // Correct scaling for requested rotations:
        // For (-90, 0, 90): Local X is thickness (0.13 base), Local Y is length (1.0 base), Local Z is height (0.56 base)
        // For (-90, 0, 0): Local X is length (1.0 base), Local Y is thickness (0.13 base), Local Z is height (0.56 base)
        
        float scaleX, scaleY, scaleZ;
        if (Mathf.Abs(euler.z - 90) < 1f) {
            scaleX = (thickness / 0.13f) * 100f;
            scaleY = (length / 1.0f) * 100f;
            scaleZ = (hScale / 0.56f) * 100f;
        } else {
            scaleX = (length / 1.0f) * 100f;
            scaleY = (thickness / 0.13f) * 100f;
            scaleZ = (hScale / 0.56f) * 100f;
        }
        
        c.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
        
        // Precise Grounding
        Renderer[] rs = c.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0) {
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float offset = -b.min.y;
            c.transform.position += new Vector3(0, offset, 0);
        }
        spawnedCurbs.Add(c);
    }

    private void CreateExitGate(LevelData.ExitGate gate)
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        float offset = 0.55f; 
        float gateY = -0.2f;
        switch (gate.side) {
            case LevelData.Side.Left: pos = new Vector3(-offset + gridOffset.x, gateY, gate.rowOrColumn + gridOffset.z + 0.5f); rot = Quaternion.Euler(0, 90, 0); break;
            case LevelData.Side.Right: pos = new Vector3(width + offset + gridOffset.x, gateY, gate.rowOrColumn + gridOffset.z + 0.5f); rot = Quaternion.Euler(0, 270, 0); break;
            case LevelData.Side.Top: pos = new Vector3(gate.rowOrColumn + gridOffset.x + 0.5f, gateY, height + offset + gridOffset.z); rot = Quaternion.Euler(0, 180, 0); break;
            case LevelData.Side.Bottom: pos = new Vector3(gate.rowOrColumn + gridOffset.x + 0.5f, gateY, -offset + gridOffset.z); rot = Quaternion.Euler(0, 0, 0); break;
        }
        if (exitGatePrefab != null) {
            GameObject gateObj = Instantiate(exitGatePrefab, pos, rot);
            gateObj.transform.localScale = Vector3.one * 0.5f;
            spawnedGates.Add(gateObj);
        }
    }

    private GameObject GetObstaclePrefab(LevelData.CarInfo info) {
        if (info.size >= 3 && truckPrefabs != null && truckPrefabs.Count > 0) return truckPrefabs[(info.position.x + info.position.y) % truckPrefabs.Count];
        if (obstaclePrefabs != null && obstaclePrefabs.Count > 0) return obstaclePrefabs[(info.position.x + info.position.y) % obstaclePrefabs.Count];
        return null;
    }

    public Vector2Int WorldToGrid(Vector3 worldPos) => new Vector2Int(Mathf.RoundToInt(worldPos.x - gridOffset.x), Mathf.RoundToInt(worldPos.z - gridOffset.z));
    public bool IsCellEmpty(int x, int z) => (x < 0 || x >= width || z < 0 || z >= height) ? false : grid[x, z] == null;

    public void UpdateGrid(CarController ignoreCar = null)
    {
        if (grid == null || grid.GetLength(0) != width || grid.GetLength(1) != height) grid = new CarController[width, height];
        System.Array.Clear(grid, 0, grid.Length);
        CarController[] allCars = Object.FindObjectsByType<CarController>(FindObjectsInactive.Exclude);
        foreach (var car in allCars) {
            if (car == ignoreCar || !car.enabled) continue;
            Vector2Int pos = WorldToGrid(car.transform.position);
            for (int i = 0; i < car.size; i++) {
                int cx = car.orientation == CarController.Orientation.Horizontal ? pos.x + i : pos.x;
                int cz = car.orientation == CarController.Orientation.Vertical ? pos.y + i : pos.y;
                if (cx >= 0 && cx < width && cz >= 0 && cz < height) grid[cx, cz] = car;
            }
        }
    }

    public bool CanExit(CarController car, Vector2Int gridPos, Vector3 direction)
    {
        if (!car.isTargetCar) return false;
        LevelData level = levels[currentLevelIndex];
        List<LevelData.ExitGate> gates = level.exitGates;
        if (gates == null || gates.Count == 0) return direction == Vector3.right && gridPos.y == 2 && gridPos.x + car.size == width;
        foreach (var g in gates) {
            if (g.side == LevelData.Side.Left && direction == Vector3.left && gridPos.x == 0 && gridPos.y == g.rowOrColumn) return true;
            if (g.side == LevelData.Side.Right && direction == Vector3.right && gridPos.x + car.size == width && gridPos.y == g.rowOrColumn) return true;
            if (g.side == LevelData.Side.Top && direction == Vector3.forward && gridPos.y + car.size == height && gridPos.x == g.rowOrColumn) return true;
            if (g.side == LevelData.Side.Bottom && direction == Vector3.back && gridPos.y == 0 && gridPos.x == g.rowOrColumn) return true;
        }
        return false;
    }

    public void NotifyTargetExiting(CarController car)
    {
        if (isLevelComplete || !spawnedCars.Contains(car.gameObject)) return;
        spawnedCars.Remove(car.gameObject);
        targetsExited++;
        car.SetExiting();
        StartCoroutine(DriveOffAnimation(car));
        if (targetsExited >= requiredExits) isLevelComplete = true;
    }

    private System.Collections.IEnumerator DriveOffAnimation(CarController car)
    {
        Vector3 dir = car.orientation == CarController.Orientation.Horizontal ? (car.transform.position.x > width / 2f ? Vector3.right : Vector3.left) : (car.transform.position.z > height / 2f ? Vector3.forward : Vector3.back);
        Vector3 start = car.transform.position;
        Vector3 target = start + dir * 5f;
        float d = 1f, t = 0;
        car.enabled = false;
        while (t < d) { car.transform.position = Vector3.Lerp(start, target, t / d); t += Time.deltaTime; yield return null; }
        Destroy(car.gameObject);
        if (isLevelComplete) CheckWin();
    }

    private void CheckWin()
    {
        if (hasTriggeredWin) return;
        hasTriggeredWin = true;
        if (winEffectPrefab) Instantiate(winEffectPrefab, new Vector3(width / 2f, 1, height / 2f), Quaternion.Euler(-90, 0, 0));
        if (winSound) { audioSource.clip = winSound; audioSource.Play(); }
        UIManager ui = Object.FindAnyObjectByType<UIManager>();
        if (ui != null) ui.ShowWinPanel();
    }

    public void ShowHint()
    {
        CarController[] cars = Object.FindObjectsByType<CarController>(FindObjectsInactive.Exclude);
        foreach (var c in cars) if (c.isTargetCar) {
            CarController b = FindBlockerInPath(c);
            if (b != null && b != c) { StartCoroutine(HighlightCar(b)); return; }
        }
    }

    private CarController FindBlockerInPath(CarController car)
    {
        Vector2Int pos = WorldToGrid(car.transform.position);
        LevelData level = levels[currentLevelIndex];
        
        foreach (var gate in level.exitGates)
        {
            if (car.orientation == CarController.Orientation.Horizontal)
            {
                if (gate.rowOrColumn != pos.y) continue;
                if (gate.side == LevelData.Side.Right)
                {
                    for (int i = pos.x + car.size; i < width; i++) if (grid[i, pos.y]) return FindBlockerInPath(grid[i, pos.y]);
                }
                else if (gate.side == LevelData.Side.Left)
                {
                    for (int i = pos.x - 1; i >= 0; i--) if (grid[i, pos.y]) return FindBlockerInPath(grid[i, pos.y]);
                }
            }
            else
            {
                if (gate.rowOrColumn != pos.x) continue;
                if (gate.side == LevelData.Side.Top)
                {
                    for (int i = pos.y + car.size; i < height; i++) if (grid[pos.x, i]) return FindBlockerInPath(grid[pos.x, i]);
                }
                else if (gate.side == LevelData.Side.Bottom)
                {
                    for (int i = pos.y - 1; i >= 0; i--) if (grid[pos.x, i]) return FindBlockerInPath(grid[pos.x, i]);
                }
            }
        }
        return car;
    }

    private System.Collections.IEnumerator HighlightCar(CarController car)
    {
        Renderer r = car.GetComponentInChildren<Renderer>();
        if (!r) yield break;
        Color orig = r.material.color;
        for (int i = 0; i < 3; i++) { r.material.color = Color.white; yield return new WaitForSeconds(0.2f); r.material.color = orig; yield return new WaitForSeconds(0.2f); }
    }

    void OnValidate()
    {
        if (levels != null && currentLevelIndex >= 0 && currentLevelIndex < levels.Count)
        {
            width = levels[currentLevelIndex].width;
            height = levels[currentLevelIndex].height;
        }
        AdjustCamera(width, height);
    }

    private void AdjustCamera(int w, int h)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
    
        mainCam.orthographic = true;

        // Center X position on the grid
        Vector3 pos = mainCam.transform.position;
        pos.x = (w / 2.0f) + gridOffset.x;
        mainCam.transform.position = pos;

        // Calculate view size to fit the grid
        // Account for camera tilt (pitch rotation)
        float aspect = mainCam.aspect;
        if (aspect <= 0.01f) aspect = 1.77f; 

        float theta = mainCam.transform.eulerAngles.x;
        float sinTilt = Mathf.Abs(Mathf.Sin(theta * Mathf.Deg2Rad));
        if (sinTilt < 0.1f) sinTilt = 1.0f;

        float margin = 1.5f; 
        float targetW = w + margin;
        float targetH = h + margin;

        float sizeByH = (targetH * sinTilt) * 0.5f;
        float sizeByW = (targetW / aspect) * 0.5f;
        
        mainCam.orthographicSize = Mathf.Max(sizeByH, sizeByW);
        
        #if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
        {
            UnityEditor.SceneView.lastActiveSceneView.AlignViewToObject(mainCam.transform);
        }
        #endif
    }
}