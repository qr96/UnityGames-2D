using UnityEngine;

[CreateAssetMenu(menuName = "GrabProto/Enemy Data", fileName = "NewEnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "Enemy";
    public float maxHP = 60f;
    public float moveSpeed = 1.8f;

    [Header("기본 공격 (단일 대상)")]
    public float attackDamage = 8f;
    public float attackRange = 1f;
    public float attackInterval = 1.2f;
}
