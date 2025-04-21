using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class Patrol2D : MonoBehaviour
{
    [Header("Patrol Settings")]
    public Transform[] waypoints;
    public float moveSpeed = 3f;
    public float waypointReachDistance = 0.1f;
    public bool loopPatrol = true;
    public float waitAtWaypoint = 1f;

    [Header("2D Visuals")]
    public bool flipSprite = true;
    public Vector2 waypointCheckOffset;

    [Header("Combat Settings")]
    public float detectionRange = 5f;
    public float attackRange = 1.5f;
    public int attackDamage = 10;
    public float attackCooldown = 2f;
    public LayerMask playerLayer;
    public int maxHealth = 30;
    public float hurtKnockbackForce = 5f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private int currentWaypoint = 0;
    private bool isWaiting = false;
    private bool movingForward = true;
    private Transform player;
    private bool isAttacking = false;
    private float lastAttackTime = 0f;
    private int currentHealth;
    private bool isHurt = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;

        if (waypoints.Length == 0)
            Debug.LogError("No waypoints set for " + gameObject.name);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No GameObject with tag 'Player' found in the scene. Enemy will not detect player.");
        }
    }


    void FixedUpdate()
    {
        if (isHurt) return;

        if (player != null && !isAttacking)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            if (distanceToPlayer <= detectionRange)
            {
                // Face the player
                if (flipSprite && spriteRenderer != null)
                    spriteRenderer.flipX = (player.position.x < transform.position.x);

                if (distanceToPlayer <= attackRange && Time.time > lastAttackTime + attackCooldown)
                {
                    StartCoroutine(AttackPlayer());
                }
                else if (distanceToPlayer > attackRange)
                {
                    ChasePlayer();
                }
                return;
            }
        }

        // If not attacking or chasing player, continue patrol
        if (!isWaiting && waypoints.Length > 0 && !isAttacking)
        {
            Patrol();
        }
    }

    void Patrol()
    {
        Vector2 targetPos = waypoints[currentWaypoint].position;
        Vector2 currentPos = (Vector2)transform.position + waypointCheckOffset;

        // Move towards waypoint
        Vector2 moveDir = (targetPos - currentPos).normalized;
        rb.velocity = moveDir * moveSpeed;

        // Update animation
        if (animator != null)
            animator.SetFloat("Speed", rb.velocity.magnitude);

        // Flip sprite if needed
        if (flipSprite && spriteRenderer != null)
            spriteRenderer.flipX = (moveDir.x < -0.1f);

        // Check if reached waypoint
        if (Vector2.Distance(currentPos, targetPos) <= waypointReachDistance)
            StartCoroutine(WaitAndMoveNext());
    }

    void ChasePlayer()
    {
        Vector2 moveDir = (player.position - transform.position).normalized;
        rb.velocity = moveDir * moveSpeed;

        if (animator != null)
            animator.SetFloat("Speed", rb.velocity.magnitude);
    }

    IEnumerator AttackPlayer()
    {
        isAttacking = true;
        rb.velocity = Vector2.zero;

        if (animator != null)
            animator.SetTrigger("Attack1");


        // Wait for attack animation to hit (you might need to adjust this timing)
        yield return new WaitForSeconds(0.3f);

        // Check if player is still in range
        if (player != null && Vector2.Distance(transform.position, player.position) <= attackRange)
        {
            // Damage player (assuming player has a PlayerHealth component)
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }
        }

        lastAttackTime = Time.time;
        yield return new WaitForSeconds(0.5f); // Additional recovery time
        isAttacking = false;
    }

    IEnumerator WaitAndMoveNext()
    {
        isWaiting = true;
        rb.velocity = Vector2.zero;

        if (animator != null)
            animator.SetFloat("Speed", 0f);

        yield return new WaitForSeconds(waitAtWaypoint);

        // Get next waypoint
        if (loopPatrol)
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        else
        {
            if (movingForward)
            {
                if (currentWaypoint >= waypoints.Length - 1)
                    movingForward = false;
                else
                    currentWaypoint++;
            }
            else
            {
                if (currentWaypoint <= 0)
                    movingForward = true;
                else
                    currentWaypoint--;
            }
        }

        isWaiting = false;
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isHurt) return;

        currentHealth -= damage;

        if (animator != null)
            animator.SetTrigger("Hurt");

        // Apply knockback
        rb.velocity = Vector2.zero;
        rb.AddForce(hitDirection * hurtKnockbackForce, ForceMode2D.Impulse);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(HurtCooldown());
        }
    }

    IEnumerator HurtCooldown()
    {
        isHurt = true;
        yield return new WaitForSeconds(0.5f);
        isHurt = false;
    }

    void Die()
    {
        if (animator != null)
            animator.SetTrigger("Die");

        // Disable all behaviors
        enabled = false;
        rb.velocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        // Destroy after animation
        Destroy(gameObject, 2f);
    }

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.2f);
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        if (loopPatrol && waypoints.Length > 1)
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);

        // Draw detection and attack ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}