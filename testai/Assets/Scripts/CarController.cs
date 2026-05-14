using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class CarController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum Orientation { Horizontal, Vertical }
    public Orientation orientation;
    public int size = 2;
    public bool isTargetCar = false;
    public AudioClip moveSound;
    private bool isExiting = false;
    private AudioSource audioSource;
    private Vector3 offset;
    private Camera mainCamera;
    private GridManager gridManager;
    private float minMoveLimit, maxMoveLimit;

    public void SetExiting() => isExiting = true;

    void Start() {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        mainCamera = Camera.main;
        gridManager = Object.FindAnyObjectByType<GridManager>();
        if (mainCamera.GetComponent<PhysicsRaycaster>() == null) mainCamera.gameObject.AddComponent<PhysicsRaycaster>();
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (gridManager == null || isExiting) return;
        offset = transform.position - GetWorldPos(eventData.position);
        CalculateMovementLimits();
        if (moveSound != null) { audioSource.clip = moveSound; audioSource.Play(); }
    }

    public void OnDrag(PointerEventData eventData) {
        if (gridManager == null || isExiting) return;
        Vector3 targetPos = GetWorldPos(eventData.position) + offset;
        MoveCar(targetPos);
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (isExiting) return;
        SnapToGrid();
        if (gridManager != null) gridManager.UpdateGrid();
    }

    private Vector3 GetWorldPos(Vector2 screenPos) {
        Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, mainCamera.WorldToScreenPoint(transform.position).z);
        return mainCamera.ScreenToWorldPoint(screenPoint);
    }

    private void CalculateMovementLimits() {
        gridManager.UpdateGrid(this); 
        Vector2Int pos = gridManager.WorldToGrid(transform.position);
        if (orientation == Orientation.Horizontal) {
            int minX = pos.x; while (minX > 0 && gridManager.IsCellEmpty(minX - 1, pos.y)) minX--;
            minMoveLimit = minX + gridManager.gridOffset.x - (gridManager.CanExit(this, new Vector2Int(minX, pos.y), Vector3.left) ? 5f : 0f);
            int maxX = pos.x; while (maxX + size < gridManager.width && gridManager.IsCellEmpty(maxX + size, pos.y)) maxX++;
            maxMoveLimit = maxX + gridManager.gridOffset.x + (gridManager.CanExit(this, new Vector2Int(maxX, pos.y), Vector3.right) ? 5f : 0f);
        } else {
            int minZ = pos.y; while (minZ > 0 && gridManager.IsCellEmpty(pos.x, minZ - 1)) minZ--;
            minMoveLimit = minZ + gridManager.gridOffset.z - (gridManager.CanExit(this, new Vector2Int(pos.x, minZ), Vector3.back) ? 5f : 0f);
            int maxZ = pos.y; while (maxZ + size < gridManager.height && gridManager.IsCellEmpty(pos.x, maxZ + size)) maxZ++;
            maxMoveLimit = maxZ + gridManager.gridOffset.z + (gridManager.CanExit(this, new Vector2Int(pos.x, maxZ), Vector3.forward) ? 5f : 0f);
        }
    }

    private void MoveCar(Vector3 targetPos) {
        Vector3 constrainedPos = transform.position;
        if (orientation == Orientation.Horizontal) {
            constrainedPos.x = Mathf.Clamp(targetPos.x, minMoveLimit, maxMoveLimit);
            if (isTargetCar && (constrainedPos.x > gridManager.width - size + 0.5f + gridManager.gridOffset.x || constrainedPos.x < gridManager.gridOffset.x - 0.5f))
                gridManager.NotifyTargetExiting(this);
        } else {
            constrainedPos.z = Mathf.Clamp(targetPos.z, minMoveLimit, maxMoveLimit);
            if (isTargetCar && (constrainedPos.z > gridManager.height - size + 0.5f + gridManager.gridOffset.z || constrainedPos.z < gridManager.gridOffset.z - 0.5f))
                gridManager.NotifyTargetExiting(this);
        }
        transform.position = constrainedPos;
    }

    private void SnapToGrid() {
        if (gridManager == null) return;
        Vector3 snapped = transform.position;
        snapped.x = Mathf.Round(snapped.x - gridManager.gridOffset.x) + gridManager.gridOffset.x;
        snapped.z = Mathf.Round(snapped.z - gridManager.gridOffset.z) + gridManager.gridOffset.z;
        if (isTargetCar) {
            float b = 0.5f;
            if (transform.position.x > gridManager.width - size + b + gridManager.gridOffset.x || transform.position.x < gridManager.gridOffset.x - b ||
                transform.position.z > gridManager.height - size + b + gridManager.gridOffset.z || transform.position.z < gridManager.gridOffset.z - b) return;
        }
        transform.position = snapped;
    }
}