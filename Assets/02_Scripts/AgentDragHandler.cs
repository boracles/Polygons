using UnityEngine;

[RequireComponent(typeof(Agent))]
public class AgentDragHandler : MonoBehaviour
{
    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 offset;   // 마우스로 누른 위치 대비 오프셋
    private Agent myAgent;    // 이 오브젝트의 Agent 컴포넌트
    private SegregationManager manager;
    
    // 마우스 입력으로 이동할 평면(지면)이 어디인지 레이어로 구분 가능
    // 예시로 LayerMask를 받거나, 그냥 y=0 Plane을 사용해도 됨.
    public LayerMask groundMask;

    // 기존 보드상의 좌표 (x, z)
    private int oldX;
    private int oldZ;

    void Awake()
    {
        mainCamera = Camera.main;
        myAgent = GetComponent<Agent>();
        manager = FindObjectOfType<SegregationManager>();
    }

    void Start()
    {
        // 에이전트 생성 시 보드에서의 좌표를 manager로부터 받아온다
        (oldX, oldZ) = manager.FindAgentPosition(myAgent);
    }

    void OnMouseDown()
    {
        // 드래그 시작
        isDragging = true;

        // 마우스 레이로 현재 월드 좌표를 구해서 오프셋 계산
        Plane plane = new Plane(Vector3.up, Vector3.zero); // y=0 plane
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        float dist;
        if (plane.Raycast(ray, out dist))
        {
            Vector3 worldPos = ray.GetPoint(dist);
            offset = transform.position - worldPos;
        }
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Plane plane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        float dist;
        if (plane.Raycast(ray, out dist))
        {
            Vector3 worldPos = ray.GetPoint(dist);
            Vector3 targetPos = worldPos + offset;
            // 드래그 중에는 살짝 y축을 띄워서 움직이도록
            targetPos.y = 1.0f;
            transform.position = targetPos;
        }
    }

    void OnMouseUp()
    {
        // 드래그 종료
        isDragging = false;

        // 마우스를 놓았을 때, 바닥(또는 Grid) 상 위치를 Raycast
        Vector2Int? cell = GetNearestEmptyCell();
        if (!cell.HasValue)
        {
            // 만약 빈 칸이 없다면, 원래 자리로 돌아가거나
            // 혹은 가장 가까운 빈칸 찾아 이동 (케이스별로 처리)
            ReturnToOldPosition();
            return;
        }

        // 보드 정보를 업데이트하고, 에이전트의 위치를 스냅
        int newX = cell.Value.x;
        int newZ = cell.Value.y;
        manager.OnAgentManualMove(myAgent, oldX, oldZ, newX, newZ);

        // 보드 내부에서의 내 좌표 갱신
        oldX = newX;
        oldZ = newZ;
    }

    private Vector2Int? GetNearestEmptyCell()
    {
        // 화면에서 Agent가 내려놓인 위치가 보드상의 어떤 (x,z)에 가까운지 계산
        // manager.width, height 범위 체크, 그리고 board[x,z]==0(빈칸)인지 확인

        Vector3 pos = transform.position;
        int x = Mathf.RoundToInt(pos.x);
        int z = Mathf.RoundToInt(-pos.z); // manager가 -z로 배치했는지 확인 필요

        if (x < 0 || x >= manager.width ||
            z < 0 || z >= manager.height)
        {
            return null; // 범위 벗어남
        }

        // 이미 누군가 있으면 불가
        if (manager.board[x, z] != 0)
        {
            return null;
        }

        return new Vector2Int(x, z);
    }

    private void ReturnToOldPosition()
    {
        // 다시 원 위치로 복귀
        Vector3 revertPos = new Vector3(oldX, 0.5f, -oldZ);
        transform.position = revertPos;
    }
}
