using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class HealthSystem : MonoBehaviour
{
    public int health;
    public Action OnDied;

    [SerializeField] int maxHealth = 100;

    private void Start()
    {
        health = maxHealth;
    }

    public void OnDamageDealt(int damage)
    {
        health -= damage;

        if (health < 0)
        {
            OnDied?.Invoke();
        }
    }
}
