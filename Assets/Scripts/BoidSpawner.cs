using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]                                      // Use Unity's Burst Compiler for increased performance
[UpdateInGroup(typeof(InitializationSystemGroup))]  // Makes the boid spawner run at the start of the ECS update cycle
                                                    // This is used here so all boids are spawned before any simulation logic
                                                    // tries to access or modify them.
public partial struct BoidSpawnerSystem : ISystem {

    // OnCreate() is the Start() method but for ECS. Gets called only once when the system is created
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<BoidSpawner>();      // Require atleast one BoidSpawner to be in ECS world
    }

    // Called every frame, just like Update(). Used for spawning the boids from all active spawners
    public void OnUpdate(ref SystemState state) {

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);    // An EntityCommandBuffer (ECB) lets the code safely create, modify or destroy entities deferred from main thread.
                                                                                // Temp allocator is used here since this ECB lives for a very short time

        // Iterate through all entities that have a BoidSpawner component
        foreach (var (spawner, entity) in SystemAPI.Query<RefRO<BoidSpawner>>().WithEntityAccess()) {

            int totalToSpawn = spawner.ValueRO.count;                           // Spawn count, from BoidSpawnerAuthoring.cs

            int groupSize = 10;                                                 // How many boids should be spawned in one group
            int numGroups = (int)math.ceil(totalToSpawn / (float)groupSize);    // How many groups

            for (int groupIndex = 0; groupIndex < numGroups; groupIndex++) {

                float3 groupCenter = UnityEngine.Random.insideUnitSphere * 20f;         // Random center position for this group within a sphere with radius 20
                float angle = UnityEngine.Random.Range(0f, 360f);                       // Random direction to face

                quaternion groupRotation = quaternion.AxisAngle(math.up(), math.radians(angle));

                // Spawn each boid in this group
                for (int i = 0; i < groupSize; i++) {

                    int spawnIndex = groupIndex * groupSize + i;
                    if (spawnIndex >= totalToSpawn)
                        break;

                    var instance = ecb.Instantiate(spawner.ValueRO.prefab);             // Create a new boid entity from the assigned prefab

                    float3 localOffset = UnityEngine.Random.insideUnitSphere * 2f;      // Position boid randomly around the group center within radius 2
                    float3 position = groupCenter + localOffset;

                    float3 forward = math.mul(groupRotation, math.forward());           // Apply group rotation to a forward vector for "facing" direction

                    // Set position and rotation
                    ecb.SetComponent(instance, LocalTransform.FromPositionRotation(position, groupRotation));
                }
            }

            ecb.DestroyEntity(entity);          // Now that the spawner is not needed anymore, it can be destroyed
        }

        ecb.Playback(state.EntityManager);      // Apply all the queued entity commands in the buffer to the world
        ecb.Dispose();                          // Dispose of the buffer to free memory
    }
}