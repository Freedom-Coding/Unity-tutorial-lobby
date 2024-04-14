using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerControls : NetworkBehaviour
{
    [SerializeField] float movementSpeedBase = 5;

    private Animator animator;
    private Rigidbody2D rb;
    private float movementSpeedMultiplier;
    private Vector2 currentMoveDirection;
    public NetworkVariable<int> playerScore = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        playerScore.OnValueChanged += FindObjectOfType<PlayerUI>().UpdateScoreUI;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        Move();
        Attack();
    }

    private void Move()
    {
        Vector2 movementDirection = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        MoveServerRPC(movementDirection, movementSpeedBase, movementSpeedMultiplier);
    }

    [ServerRpc]
    private void MoveServerRPC(Vector2 movementDirection, float movementSpeedBase, float movementSpeedMultiplier)
    {
        Vector2 moveVector = movementDirection.normalized * movementSpeedBase * movementSpeedMultiplier;

        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();

        animator.SetFloat("Speed", moveVector.magnitude);
        rb.velocity = moveVector;

        if (moveVector != Vector2.zero)
        {
            currentMoveDirection = new Vector2(moveVector.normalized.x, moveVector.normalized.y);
            animator.SetFloat("Horizontal", moveVector.normalized.x);
            animator.SetFloat("Vertical", moveVector.normalized.y);
        }
    }


    private bool canAttack = true;
    private void Attack()
    {
        if (Input.GetMouseButton(0))
        {
            movementSpeedMultiplier = 0.5f;
            animator.SetFloat("Attack", 1);

            if (canAttack)
            {
                AttackServerRPC(currentMoveDirection);

                StartCoroutine(AttackCooldown());
            }
        }
        else
        {
            animator.SetFloat("Attack", 0);
            movementSpeedMultiplier = 1f;
        }
    }

    [ServerRpc]
    private void AttackServerRPC(Vector2 currentMoveDirection)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, 1f, currentMoveDirection, 0, 1 << 6);

        if (hits.Length > 0)
        {
            hits[0].transform.GetComponent<HealthSystem>().OnDamageDealt(50);
            if (hits[0].transform.GetComponent<HealthSystem>().health < 0)
            {
                playerScore.Value++;
            }
        }
    }

    private IEnumerator AttackCooldown()
    {
        canAttack = false;
        yield return new WaitForSeconds(1);
        canAttack = true;
    }
}