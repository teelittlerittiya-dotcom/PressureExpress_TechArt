using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleManager : MonoBehaviour
{
    [Header("Persistent Status Effects")]
    [SerializeField] private Transform effectAnchor;

    // ------------------------------------------------------------------
    // POOLING SYSTEM DATA
    // ------------------------------------------------------------------
    private Dictionary<int, Queue<GameObject>> poolDictionary = new Dictionary<int, Queue<GameObject>>();
    private Dictionary<int, int> instanceToPrefabId = new Dictionary<int, int>();

    // เก็บ Particle แบบ Loop ที่กำลังเล่นอยู่ (Key = Module Type)
    private Dictionary<System.Type, GameObject> activeLoopingParticles = new Dictionary<System.Type, GameObject>();
    private Dictionary<System.Type, GameObject> activeLoopingPrefabs = new Dictionary<System.Type, GameObject>();

    private Transform EffectAnchor => effectAnchor != null ? effectAnchor : transform;

    public void SetEffectAnchor(Transform anchor)
    {
        effectAnchor = anchor;
    }

    // -----------------------------------------------------------------------
    // 1. จัดการ Particle แบบต่อเนื่อง (State Change)
    // -----------------------------------------------------------------------
    public void UpdateStateParticle(System.Type moduleType, GameObject targetPrefab)
    {
        bool hasActive = activeLoopingParticles.ContainsKey(moduleType) && activeLoopingParticles[moduleType] != null;
        GameObject currentPrefab = activeLoopingPrefabs.ContainsKey(moduleType) ? activeLoopingPrefabs[moduleType] : null;

        // กรณี A: สั่งปิด (Target เป็น Null)
        if (targetPrefab == null)
        {
            if (hasActive)
            {
                ReturnToPool(activeLoopingParticles[moduleType]); 
                activeLoopingParticles.Remove(moduleType);
                activeLoopingPrefabs.Remove(moduleType);
            }
            return;
        }

        // กรณี B: เหมือนเดิม ไม่ต้องทำอะไร
        if (hasActive && currentPrefab == targetPrefab) return;

        // กรณี C: เปลี่ยนตัวใหม่
        if (hasActive)
        {
            ReturnToPool(activeLoopingParticles[moduleType]);
        }

        // --- ดึงจาก Pool ---
        GameObject newParticle = GetFromPool(targetPrefab);
        
        // --- Setup (State Particle ต้องเกาะติด Cargo เสมอ) ---
        // หมายเหตุ: การ GetFromPool จะ SetParent(transform) ให้อยู่แล้ว 
        // แต่เรียกซ้ำเพื่อความชัวร์และ Reset transform
        newParticle.transform.SetParent(EffectAnchor);
        newParticle.transform.localPosition = Vector3.zero;
        newParticle.transform.localRotation = Quaternion.identity;
        newParticle.transform.localScale = Vector3.one; 

        if (activeLoopingParticles.ContainsKey(moduleType))
        {
            activeLoopingParticles[moduleType] = newParticle;
            activeLoopingPrefabs[moduleType] = targetPrefab;
        }
        else
        {
            activeLoopingParticles.Add(moduleType, newParticle);
            activeLoopingPrefabs.Add(moduleType, targetPrefab);
        }
    }

    public void ClearStateParticles()
    {
        List<GameObject> active = new List<GameObject>(activeLoopingParticles.Values);
        foreach (GameObject particle in active) ReturnToPool(particle);
        activeLoopingParticles.Clear();
        activeLoopingPrefabs.Clear();
    }

    // -----------------------------------------------------------------------
    // INTERNAL POOLING LOGIC (Clean Hierarchy)
    // -----------------------------------------------------------------------
    
    private GameObject GetFromPool(GameObject prefab)
    {
        int prefabId = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(prefabId))
        {
            poolDictionary.Add(prefabId, new Queue<GameObject>());
        }

        GameObject objectToSpawn = null;
        
        while (poolDictionary[prefabId].Count > 0)
        {
            GameObject candidate = poolDictionary[prefabId].Dequeue();
            if (candidate != null)
            {
                objectToSpawn = candidate;
                break;
            }
        }

        if (objectToSpawn == null)
        {
            // [Fix] สร้างแล้วให้เป็นลูกของ Cargo ทันที เพื่อไม่ให้รก Scene
            objectToSpawn = Instantiate(prefab, EffectAnchor);
            instanceToPrefabId[objectToSpawn.GetInstanceID()] = prefabId;
        }
        else
        {
            // ถ้าดึงจาก Pool ก็ให้มั่นใจว่าเป็นลูกของเรา (กรณี Impact ที่เคย Detach ไป)
            objectToSpawn.transform.SetParent(EffectAnchor);
        }

        objectToSpawn.SetActive(true);
        
        foreach(var ps in objectToSpawn.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop();
            ps.Play();
        }

        return objectToSpawn;
    }

    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        
        // [Fix] เก็บกลับมาเป็นลูกของ Cargo เพื่อซ่อนใน Hierarchy
        obj.transform.SetParent(EffectAnchor);
        obj.transform.localPosition = Vector3.zero; // รีเซ็ตตำแหน่งให้สวยงาม

        int instanceId = obj.GetInstanceID();
        
        if (instanceToPrefabId.ContainsKey(instanceId))
        {
            int prefabId = instanceToPrefabId[instanceId];
            if (!poolDictionary.ContainsKey(prefabId))
                poolDictionary[prefabId] = new Queue<GameObject>();
            
            poolDictionary[prefabId].Enqueue(obj);
        }
        else
        {
            Destroy(obj); // ถ้าไม่รู้ที่มา ก็ทำลายทิ้ง
        }
    }

}
