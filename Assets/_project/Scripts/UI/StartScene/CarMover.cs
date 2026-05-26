using UnityEngine;

public class CarMover : MonoBehaviour
{
    [Header("이동 경로")]
    [SerializeField] private Vector2 startPoint;
    [SerializeField] private Vector2 endPoint;

    [Header("이동 설정")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private float loopDelay = 0f;

    [Header("스프라이트")]
    [SerializeField] private bool flipSpriteByDirection = true;

    [Header("랜덤화")]
    [SerializeField] private bool randomizeSpeed = false;
    [SerializeField] private Vector2 speedRange = new Vector2(2f, 4f);
    [SerializeField] private bool randomizeStartPosition = true;

    private SpriteRenderer spriteRenderer;
    private float currentDelay;
    private bool isMoving;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (randomizeSpeed)
            speed = Random.Range(speedRange.x, speedRange.y);
    }

    private void Start()
    {
        if (randomizeStartPosition)
        {
            float t = Random.value;
            transform.position = Vector2.Lerp(startPoint, endPoint, t);
            isMoving = true;
        }
        else
        {
            transform.position = startPoint;
            currentDelay = startDelay;
            isMoving = (startDelay <= 0f);
        }

        ApplySpriteFlip();
    }

    private void Update()
    {
        if (currentDelay > 0f)
        {
            currentDelay -= Time.deltaTime;
            if (currentDelay <= 0f) isMoving = true;
            return;
        }

        if (!isMoving) return;

        transform.position = Vector2.MoveTowards(
            transform.position,
            endPoint,
            speed * Time.deltaTime
        );

        if (Vector2.Distance(transform.position, endPoint) < 0.01f)
        {
            transform.position = startPoint;
            if (loopDelay > 0f)
            {
                currentDelay = loopDelay;
                isMoving = false;
            }
        }
    }

    private void ApplySpriteFlip()
    {
        if (!flipSpriteByDirection || spriteRenderer == null) return;
        spriteRenderer.flipX = endPoint.x < startPoint.x;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(startPoint, 0.3f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(endPoint, 0.3f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPoint, endPoint);
    }
}