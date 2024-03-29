﻿using System.Collections;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드 가져오기

// 좀비 AI 구현
public class Zombie : LivingEntity
{
    public LayerMask        whatIsTarget;       // 추적 대상 레이어

    private LivingEntity    targetEntity;       // 추적 대상
    private NavMeshAgent    navMeshAgent;       // 경로 계산 AI 에이전트

    public ParticleSystem   hitEffect;          // 피격 시 재생할 파티클 효과
    public AudioClip        deathSound;         // 사망 시 재생할 소리
    public AudioClip        hitSound;           // 피격 시 재생할 소리

    private Animator        zombieAnimator;     // 애니메이터 컴포넌트
    private AudioSource     zombieAudioPlayer;  // 오디오 소스 컴포넌트
    private Renderer        zombieRenderer;     // 렌더러 컴포넌트

    public  float           damage = 20f;       // 공격력
    public  float           timeBetAttack = 0.5f; // 공격 간격
    private float           lastAttackTime;     // 마지막 공격 시점

    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    private bool hasTarget {
        get
        {
            // 추적할 대상이 존재하고, 대상이 사망하지 않았다면 true
            if (targetEntity != null && !targetEntity.dead)
            {
                return true;
            }

            // 그렇지 않다면 false
            return false;
        }
    }

    private void Awake() 
    {
        // 초기화
        navMeshAgent        = GetComponent<NavMeshAgent>();
        zombieAnimator      = GetComponent<Animator>();
        zombieAudioPlayer   = GetComponent<AudioSource>();
        zombieRenderer      = GetComponentInChildren<Renderer>();

    }

    // 좀비 AI의 초기 스펙을 결정하는 셋업 메서드
    public void Setup(ZombieData zombieData) 
    {
        // 체력 설정
        startingHealth = zombieData.health;
        health = zombieData.health;
        // 공격력 설정
        damage = zombieData.damage;
        // 내비메시 에이전트의 이동 속도 설정
        navMeshAgent.speed = zombieData.speed;
        // 랜더러가 사용 중인 머터리얼의 컬러를 변경, 외형 색이 변합
        zombieRenderer.material.color = zombieData.skinColor;
    }

    private void Start() 
    {
        // 게임 오브젝트 활성화와 동시에 AI의 추적 루틴 시작
        StartCoroutine(UpdatePath());
    }

    private void Update() 
    {
        // 추적 대상의 존재 여부에 따라 다른 애니메이션 재생
        zombieAnimator.SetBool("HasTarget", hasTarget);
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로 갱신
    private IEnumerator UpdatePath() 
    {
        // 살아 있는 동안 무한 루프
        while (!dead)
        {
            if (hasTarget)
            {
                // 추적 대상 존재 : 경로를 갱신하고 AI 이동을 계속 진행
                // navMeshAgent.isStopped -> AI를 멈출건지 아닌지를 판단.
                // SetDestination -> 추적대상의 위치를 입력받아 이동경로를 갱신한다.
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(targetEntity.transform.position);
            }
            else
            {
                // 추적 대상 없음 : AI 이동 중지
                navMeshAgent.isStopped = true;

                // 20 유닛의 반지름을 가진 가상의 구를 그렸을 때 구와 겹치는 모든 콜라이더를 가져온다.
                // 단, whatIsTarget 레이어를 가진 콜라이더만 가져오도록 필터링 한다.
                Collider[] colliders = Physics.OverlapSphere(transform.position, 20f, whatIsTarget);

                // 모든 콜라이더를 순회하면서 살아 있는 LivingEntity 찾기
                for (int i = 0; i < colliders.Length; i++)
                {
                    // 콜라이더로부터 LivingENtity 컴포넌트 가져오기
                    LivingEntity livingEntity = colliders[i].GetComponent<LivingEntity>();

                    if (livingEntity != null && !livingEntity.dead)
                    {
                        // 추적 대상을 해당 LivingEntity로 설정
                        targetEntity = livingEntity;
                    }

                    // for 문 루프 즉시 정지.
                    break;
                }
            }
            // 0.25초 주기로 처리 반복
            yield return new WaitForSeconds(0.25f);
        }
    }

    // 데미지를 입었을 때 실행할 처리
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) 
    {
        if (!dead)
        {
            // 공격받은 지점과 방향으로 파티클 재생
            hitEffect.transform.position = hitPoint;
            hitEffect.transform.rotation = Quaternion.LookRotation(hitNormal);
            hitEffect.Play();

            // 피격 효과음 재생
            zombieAudioPlayer.PlayOneShot(hitSound);
        }


        // LivingEntity의 OnDamage()를 실행하여 데미지 적용
        base.OnDamage(damage, hitPoint, hitNormal);
    }

    // 사망 처리
    public override void Die() 
    {
        // LivingEntity의 Die()를 실행하여 기본 사망 처리 실행
        base.Die();

        // 다른 AI를 방해하지 않도록 Die()를 실행하여 기본 사망 처리 실행
        Collider[] zombieColliders = GetComponents<Collider>();
        for (int i = 0; i < zombieColliders.Length; i++)
        {
            zombieColliders[i].enabled = false;
        }

        // AI 추적을 중지하고 내비메시 컴포넌트 비활성화
        navMeshAgent.isStopped = true;
    }

    private void OnTriggerStay(Collider other) 
    {
        // 트리거 충돌한 상대방 게임 오브젝트가 추적 대상이라면 공격 실행
        // 자신이 사망하지 않았으며,
        // 최근 공격 시점에서 timeBetAttack 이상 시간이 지났다면 공격가능
        if (!dead && Time.time >= lastAttackTime + timeBetAttack)
        {
            // 상대방의 LivingEntity 타입 가져오기 시도
            LivingEntity attackTarget = other.GetComponent<LivingEntity>();

            // 상대방의 LivingEntity가 자신의 추적 대상이라면 공격 실행
            if (attackTarget != null && attackTarget == targetEntity)
            {
                // 최근 공격시간 갱신
                lastAttackTime = Time.time;

                // 상대방의 피격위치와 피격 방향을 근사값으로 계산
                // ClosestPoint() -> 콜라이더 표면 위의 점중 특정 위치와 가장 가까운 점을 반환
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = transform.position - other.transform.position;

                // 공격실행
                attackTarget.OnDamage(damage, hitPoint, hitNormal);
            }
        }
    }
}