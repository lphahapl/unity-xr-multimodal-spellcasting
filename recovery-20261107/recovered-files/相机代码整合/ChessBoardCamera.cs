using UnityEngine;

public class ChessBoardCamera : MonoBehaviour
{
    [Header("摄像机相对人物的偏移")]
    public Vector3 offset;
    [Header("摄像机相对人物的旋转")]
    public Vector3 rotate;
    public ChessBoardContorllor contorllor;
    public bool isDragging;
    public float height;
    public bool isLocked = true;
    public float mouseSensitive = 4f;
    public float rotateSensitive = 20f;
    public float lowest = 1f;
    public float highest = 10f;
    public float ySpeed = 1f;

    [Header("摄像机避障")]
    public float pivotHeight = 1.5f;
    public LayerMask barrierLayer = Physics.DefaultRaycastLayers;

    private float yaw;
    private Vector3 dragingOffset;

    private void Awake()
    {
        if (contorllor == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                contorllor = player.GetComponent<ChessBoardContorllor>();
        }
    }

    private void Update()
    {
        if (contorllor == null) return;

        float hori = Input.GetAxis("Mouse X");
        float verti = Input.GetAxis("Mouse Y");
        float scroll = Input.mouseScrollDelta.y;

        bool isPanning = Input.GetMouseButton(0) && Input.GetMouseButton(1);
        bool isRotating = Input.GetMouseButton(2) && !isPanning;
        isDragging = isPanning || isRotating;
        isLocked = !isDragging;

        if (isPanning)
        {
            dragingOffset += new Vector3(hori, 0f, verti)
                * mouseSensitive * Time.deltaTime;
        }
        else if (isRotating)
        {
            yaw += hori * rotateSensitive * Time.deltaTime;
        }
        else
        {
            yaw = 0f;
            dragingOffset = Vector3.zero;
        }

        if (scroll != 0f)
            offset.y = Mathf.Clamp(offset.y + scroll * ySpeed, lowest, highest);
    }

    private void LateUpdate()
    {
        if (contorllor == null) return;

        Transform player = contorllor.transform;
        Quaternion baseRotation = Quaternion.Euler(rotate);
        Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRotation;
        Vector3 pivot = player.position + Vector3.up * pivotHeight;

        // 保留原 Inspector 中 offset 的含义：未拖动时仍是人物位置 + offset。
        // 先换算到默认相机旋转的局部空间，再用当前旋转计算绕枢轴的位置。
        Vector3 localOffset = Quaternion.Inverse(baseRotation)
            * (offset - Vector3.up * pivotHeight);
        Vector3 desiredPos = pivot + rotation * localOffset + dragingOffset;
        Vector3 dir = desiredPos - pivot;

        // 只调整最终显示位置，不将避障后的距离写回偏移，障碍消失后可恢复。
        if (Physics.Linecast(pivot, desiredPos, out RaycastHit hit,
            barrierLayer, QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
            desiredPos = pivot + dir.normalized * safeDistance;
        }

        transform.position = desiredPos;
        transform.rotation = rotation;
    }
}
