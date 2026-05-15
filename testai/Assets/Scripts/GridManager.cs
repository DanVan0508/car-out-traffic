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
    public GameObject curbCornerPrefab;
    public GameObject exitGatePrefab;
    public GameObject parkingSlotPrefab;
    public List<GameObject> environmentPrefabs;
    public GameObject winEffectPrefab;
public AudioClip winSound;
    private AudioSource audioSource;

    private GameObject spawnedEnvironment;
    private List<GameObject> spawnedGates = new List<GameObject>();
    private List<GameObject> spawnedCurbs = new List<GameObject>();
private List<GameObject> spawnedSlots = new List<GameObject>();
    private List<GameObject> spawnedCars = new List<GameObject>();
    private CarController[,] grid;
    private int targetsExited = 0;
    private int requiredExits = 0;
    private bool isLevelComplete = false;
    private bool hasTriggeredWin = false;
    private const string LEVEL_KEY = "CurrentLevelIndex";

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

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
        SpawnEnvironment(width, height);
        SpawnPerimeter(level);
        SpawnParkingSlots(width, height);

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

    private void SpawnParkingSlots(int w, int h)
    {
        if (parkingSlotPrefab == null) return;
        
        GameObject container = new GameObject("ParkingSlotsContainer");
        spawnedSlots.Add(container);
        container.transform.position = gridOffset + new Vector3(0, -0.02f, 0);

        // Instantiate a temporary object to calibrate scaling and offset
        GameObject temp = Instantiate(parkingSlotPrefab, Vector3.zero, Quaternion.Euler(-90, 0, 0));
        Renderer r = temp.GetComponentInChildren<Renderer>();
        if (r == null) {
            DestroyImmediate(temp);
            return;
        }

        // We want the bounds to be exactly 1x1 in world XZ
        Vector3 initialSize = r.bounds.size;
        float targetScaleX = 1.0f / initialSize.x;
        float targetScaleZ = 1.0f / initialSize.z;
        
        // Apply the scale to the temp object to check the offset
        temp.transform.localScale = new Vector3(temp.transform.localScale.x * targetScaleX, temp.transform.localScale.y * targetScaleZ, temp.transform.localScale.z);
        
        // After scaling, find the offset needed to put the min corner at (0, 0)
        // Since it's at (0,0,0) world, its bounds.min is its offset from pivot
        Vector3 offsetToMin = r.bounds.min;
        DestroyImmediate(temp);

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                GameObject slot = Instantiate(parkingSlotPrefab, Vector3.zero, Quaternion.Euler(-90, 0, 0));
                slot.transform.SetParent(container.transform);
                
                // Set scale first
                slot.transform.localScale = new Vector3(slot.transform.localScale.x * targetScaleX, slot.transform.localScale.y * targetScaleZ, slot.transform.localScale.z);
                
                // Position so that the corner is at (x, z)
                // WorldPos = PivotPos + OffsetToMin -> PivotPos = WorldPos - OffsetToMin
                // We want WorldPos (min) to be (x, 0.01, z)
                Vector3 targetPivotPos = new Vector3(x, 0.01f, z) - new Vector3(offsetToMin.x, 0, offsetToMin.z);
                
                slot.transform.localPosition = targetPivotPos;
                slot.name = $"ParkingSlot_{x}_{z}";
                spawnedSlots.Add(slot);
            }
        }
    }

    private void SpawnEnvironment(int w, int h)
    {
        if (spawnedEnvironment != null)
        {
            if (Application.isPlaying) Destroy(spawnedEnvironment);
            else DestroyImmediate(spawnedEnvironment);
        }

        if (environmentPrefabs == null || environmentPrefabs.Count == 0) return;

        // Logic: 6x6 -> 1, 8x8 -> 2, 10x10 -> 3
        int index = (w - 6) / 2;
        if (index < 0) index = 0;
        if (index >= environmentPrefabs.Count) index = environmentPrefabs.Count - 1;

        GameObject prefab = environmentPrefabs[index];
        if (prefab != null)
        {
            // Match the location, rotation and scale of the prefab, while respecting gridOffset
            spawnedEnvironment = Instantiate(prefab, prefab.transform.position + gridOffset, prefab.transform.rotation);
            spawnedEnvironment.transform.localScale = prefab.transform.localScale;
            spawnedEnvironment.name = "CityEnvironment";
        }
    }

    private void Cleanup()
    {
        if (spawnedEnvironment != null)
        {
            if (Application.isPlaying) Destroy(spawnedEnvironment);
            else DestroyImmediate(spawnedEnvironment);
            spawnedEnvironment = null;
        }

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
        if (spawnedSlots != null) {
            foreach (var go in spawnedSlots) if (go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            spawnedSlots.Clear();
        }

        CarController[] legacyCars = Object.FindObjectsByType<CarController>(FindObjectsInactive.Include);
        foreach (var c in legacyCars) if (c && c.gameObject) { if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject); }
        
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allObjects) {
            if (go == null) continue;
            string n = go.name;
            if (n.Contains("Curb") || n.Contains("ExitGate") || n.Contains("ModelPivot") || n.Contains("NewCar") || 
                n.Contains("VLine") || n.Contains("HLine") || n.Contains("Num_") || n.Contains("Barrier") || n.Contains("ParkingSlot")) {
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
        
        float thickness = 0.46f;
        float hScale = 0.46f; 
        float outerOffset = thickness * 0.5f;
        float padding = 0.06f; // Adjusted to 0.06 units

        float brickLengthX = (width + 2 * outerOffset) / (float)width - padding;
        float brickLengthZ = (height + 2 * outerOffset) / (float)height - padding;

        for (int x = 0; x < width; x++) {
            if (!HasGate(LevelData.Side.Bottom, x)) SpawnCurb(curbPrefab, new Vector3(x + gridOffset.x + 0.5f, 0, -outerOffset + gridOffset.z), new Vector3(90, 90, 0), thickness, hScale, brickLengthX);
            if (!HasGate(LevelData.Side.Top, x)) SpawnCurb(curbPrefab, new Vector3(x + gridOffset.x + 0.5f, 0, level.height + outerOffset + gridOffset.z), new Vector3(90, 90, 0), thickness, hScale, brickLengthX);
        }
        for (int z = 0; z < height; z++) {
            if (!HasGate(LevelData.Side.Left, z)) SpawnCurb(curbPrefab, new Vector3(-outerOffset + gridOffset.x, 0, z + gridOffset.z + 0.5f), new Vector3(90, 0, 0), thickness, hScale, brickLengthZ);
            if (!HasGate(LevelData.Side.Right, z)) SpawnCurb(curbPrefab, new Vector3(width + outerOffset + gridOffset.x, 0, z + gridOffset.z + 0.5f), new Vector3(90, 0, 0), thickness, hScale, brickLengthZ);
        }
        
        GameObject cp = curbCornerPrefab != null ? curbCornerPrefab : curbPrefab;
        float cornerPadding = padding * 0.5f;
        // Bottom-Left
        SpawnCurb(cp, new Vector3(-outerOffset + gridOffset.x, 0, -outerOffset + gridOffset.z), new Vector3(90, 0, 0), thickness - padding, hScale, thickness - padding);
        // Bottom-Right
        SpawnCurb(cp, new Vector3(width + outerOffset + gridOffset.x, 0, -outerOffset + gridOffset.z), new Vector3(90, 270, 0), thickness - padding, hScale, thickness - padding);
        // Top-Left
        SpawnCurb(cp, new Vector3(-outerOffset + gridOffset.x, 0, level.height + outerOffset + gridOffset.z), new Vector3(90, 90, 0), thickness - padding, hScale, thickness - padding);
        // Top-Right
        SpawnCurb(cp, new Vector3(width + outerOffset + gridOffset.x, 0, level.height + outerOffset + gridOffset.z), new Vector3(90, 180, 0), thickness - padding, hScale, thickness - padding);
        }

        private bool HasGate(LevelData.Side side, int idx) {
        LevelData level = levels[currentLevelIndex];
        if (level.exitGates == null || level.exitGates.Count == 0) return side == LevelData.Side.Right && idx == 2;
        foreach (var g in level.exitGates) if (g.side == side && g.rowOrColumn == idx) return true;
        return false;
        }

            private void SpawnCurb(GameObject prefab, Vector3 pos, Vector3 euler, float thickness, float hScale, float length) {
        if (prefab == null) return;
        GameObject c = Object.Instantiate(prefab, pos, Quaternion.identity);
        c.transform.localEulerAngles = euler;
        c.transform.localScale = Vector3.one;
        
        Renderer r = c.GetComponentInChildren<Renderer>();
        if (r != null) {
            Vector3 naturalSize = r.bounds.size;
            bool isHorizontal = Mathf.Abs(euler.y - 90) < 1f || Mathf.Abs(euler.y - 270) < 1f;
            float targetWX = isHorizontal ? length : thickness;
            float targetWZ = isHorizontal ? thickness : length;
            float targetWY = hScale;

            Vector3 s = Vector3.one;
            Vector3 lX = c.transform.TransformDirection(Vector3.right);
            Vector3 lY = c.transform.TransformDirection(Vector3.up);
            Vector3 lZ = c.transform.TransformDirection(Vector3.forward);

            if (Mathf.Abs(lX.x) > 0.5f) s.x = naturalSize.x > 0 ? targetWX / naturalSize.x : 1f;
            else if (Mathf.Abs(lX.y) > 0.5f) s.x = naturalSize.y > 0 ? targetWY / naturalSize.y : 1f;
            else if (Mathf.Abs(lX.z) > 0.5f) s.x = naturalSize.z > 0 ? targetWZ / naturalSize.z : 1f;

            if (Mathf.Abs(lY.x) > 0.5f) s.y = naturalSize.x > 0 ? targetWX / naturalSize.x : 1f;
            else if (Mathf.Abs(lY.y) > 0.5f) s.y = naturalSize.y > 0 ? targetWY / naturalSize.y : 1f;
            else if (Mathf.Abs(lY.z) > 0.5f) s.y = naturalSize.z > 0 ? targetWZ / naturalSize.z : 1f;

            if (Mathf.Abs(lZ.x) > 0.5f) s.z = naturalSize.x > 0 ? targetWX / naturalSize.x : 1f;
            else if (Mathf.Abs(lZ.y) > 0.5f) s.z = naturalSize.y > 0 ? targetWY / naturalSize.y : 1f;
            else if (Mathf.Abs(lZ.z) > 0.5f) s.z = naturalSize.z > 0 ? targetWZ / naturalSize.z : 1f;

            c.transform.localScale = s;
        }
        
        Renderer[] rs = c.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0) {
            Bounds b = rs[0].bounds;
            foreach (var renderer in rs) b.Encapsulate(renderer.bounds);
            float offset = -b.min.y;
            c.transform.position += Vector3.zero;
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
    #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null) SpawnEnvironment(width, height);
            };
        }
    #endif
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