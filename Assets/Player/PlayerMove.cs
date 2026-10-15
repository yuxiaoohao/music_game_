using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家移动控制：支持左右 + 上下移动、边界限制，以及鼠标左键闪现
/// </summary>
public class PlayerMove : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("移动速度（单位/秒）")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("朝向设置")]
    [Tooltip("角色贴图中头部所在的初始角度（度）。90=头部朝上（贴图的上边），0=头部朝右，-90=头部朝下")]
    [SerializeField] private float spriteHeadAngle = 90f;
    [Tooltip("头部转向运动方向的角速度（度/秒），越大转身越干脆")]
    [SerializeField] private float rotateSpeed = 720f;

    [Header("移动边界")]
    [Tooltip("X 轴最小值（左边界）")]
    [SerializeField] private float minX = -8.5f;
    [Tooltip("X 轴最大值（右边界）")]
    [SerializeField] private float maxX = 8.5f;
    [Tooltip("Y 轴最小值（下边界）")]
    [SerializeField] private float minY = -4.5f;
    [Tooltip("Y 轴最大值（上边界）")]
    [SerializeField] private float maxY = 4.5f;

    [Header("闪现设置")]
    [Tooltip("触发闪现的鼠标按键：0=左键，1=右键，2=中键")]
    [SerializeField] private int dashMouseButton = 0;
    [Tooltip("闪现的距离（单位）")]
    [SerializeField] private float dashDistance = 3f;
    [Tooltip("闪现过程持续的时间（秒），越小越像瞬间闪现")]
    [SerializeField] private float dashDuration = 0.12f;
    [Tooltip("两次闪现之间的冷却时间（秒）")]
    [SerializeField] private float dashCooldown = 0.02f;
    [Tooltip("开启后闪现方向为鼠标所在位置，关闭后为角色当前朝向")]
    [SerializeField] private bool dashToMouse = false;

    // 摄像机缓存，用于把鼠标屏幕坐标转换成世界坐标
    private Camera mainCamera;

    // 角色当前的朝向（最后一次移动的方向），默认朝右
    private Vector3 facingDirection = Vector3.right;

    // 闪现状态
    private bool isDashing = false;
    private float nextDashTime = 0f;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    /// <summary>
    /// 每帧读取输入，处理闪现与普通移动
    /// </summary>
    private void Update()
    {
        // 闪现过程中忽略移动输入，避免打断闪现
        if (isDashing)
        {
            return;
        }

        HandleDashInput();

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 归一化方向向量，避免斜向移动时速度叠加变快
        Vector3 direction = new Vector3(horizontal, vertical, 0f).normalized;

        // 记录朝向，闪现时作为默认方向使用
        // 头部始终朝向运动方向，静止时保持上一次的朝向
        if (direction != Vector3.zero)
        {
            facingDirection = direction;
            FaceDirection(direction);
        }

        // 乘以 Time.deltaTime 保证移动速度与帧率无关
        transform.Translate(direction * moveSpeed * Time.deltaTime);

        // 移动后立刻限制位置，防止角色越界
        ClampPosition();
    }

    /// <summary>
    /// 让角色头部转向指定方向（仅绕 Z 轴旋转，保证 2D 平面朝向正确）
    /// </summary>
    private void FaceDirection(Vector3 direction)
    {
        if (direction == Vector3.zero)
        {
            return;
        }

        // 运动方向在世界坐标下的角度
        float moveAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 减去头部初始角度，得到需要旋转到的 Z 轴角度
        float targetAngle = moveAngle - spriteHeadAngle;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

        // 平滑转向，rotateSpeed 很大时近似瞬间对齐
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 检测闪现输入：点击鼠标左键后朝鼠标位置（或当前朝向）闪现
    /// </summary>
    private void HandleDashInput()
    {
        // 冷却中或未点击闪现按键，直接返回
        if (Time.time < nextDashTime || !Input.GetMouseButtonDown(dashMouseButton))
        {
            return;
        }

        Vector3 dashDirection = dashToMouse ? GetMouseDirection() : facingDirection;

        StartCoroutine(DashCoroutine(dashDirection));
    }

    /// <summary>
    /// 获取从角色指向鼠标位置的方向（仅取 XY 平面）
    /// </summary>
    private Vector3 GetMouseDirection()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 toMouse = mouseWorldPos - transform.position;
        toMouse.z = 0f;

        // 鼠标与角色重合时，退化为使用当前朝向
        return toMouse == Vector3.zero ? facingDirection : toMouse.normalized;
    }

    /// <summary>
    /// 把鼠标的屏幕坐标转换为世界坐标
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }

    /// <summary>
    /// 闪现协程：在 dashDuration 时间内把角色插值移动到目标点
    /// </summary>
    private IEnumerator DashCoroutine(Vector3 direction)
    {
        isDashing = true;

        Vector3 dashDirection = direction.normalized;

        // 闪现时头部也朝向闪现方向
        facingDirection = dashDirection;

        Vector3 start = transform.position;
        Vector3 target = start + dashDirection * dashDistance;

        // 闪现终点同样受边界限制
        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.y = Mathf.Clamp(target.y, minY, maxY);

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            transform.position = Vector3.Lerp(start, target, t);
            FaceDirection(dashDirection);
            yield return null;
        }

        transform.position = target;
        isDashing = false;
        nextDashTime = Time.time + dashCooldown;
    }

    /// <summary>
    /// 将角色位置限制在设定的边界范围内
    /// </summary>
    private void ClampPosition()
    {
        Vector3 position = transform.position;

        // 限制左右边界
        position.x = Mathf.Clamp(position.x, minX, maxX);

        // 限制上下边界
        position.y = Mathf.Clamp(position.y, minY, maxY);

        transform.position = position;
    }
}
