using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// I recommend reading the comments of this script last since this depends on the reader knowing some ecs stuff
// because I can't be bothered to retype them here

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct BoidMovementSystem : ISystem {

    // Updated every frame
    public void OnUpdate(ref SystemState state) {

        var deltaTime = SystemAPI.Time.DeltaTime;

        float3 center = new float3(0, 0, 0);
        float boundsSize = 40f;

        var entityQuery = SystemAPI.QueryBuilder()
            .WithAll<Boid, LocalTransform>()
            .Build();

        var boidEntities = entityQuery.ToEntityArray(Allocator.Temp);
        var boidTransforms = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var boidVelocities = entityQuery.ToComponentDataArray<Boid>(Allocator.Temp);

        var newVelocities = new NativeArray<float3>(boidEntities.Length, Allocator.Temp);

        for (int i = 0; i < boidEntities.Length; i++) {

            float3 currentPos = boidTransforms[i].Position;
            float3 currentVel = boidVelocities[i].velocity;

            float3 separation = float3.zero;
            float3 alignment = float3.zero;
            float3 cohesion = float3.zero;

            int neighborCount = 0;

            for (int j = 0; j < boidEntities.Length; j++) {

                if (i == j) continue;

                float3 otherPos = boidTransforms[j].Position;
                float3 otherVel = boidVelocities[j].velocity;

                float dist = math.distance(currentPos, otherPos);
                if (dist < 4f) {
                    separation += (currentPos - otherPos) / math.max(dist, 0.01f);
                    alignment += otherVel;
                    cohesion += otherPos;
                    neighborCount++;
                }
            }

            if (neighborCount > 0) {
                separation /= neighborCount;
                alignment /= neighborCount;
                cohesion = (cohesion / neighborCount - currentPos);
            }

            float3 boundaryAvoidance = float3.zero;
            float boundaryThreshold = boundsSize * 0.6f;
            float boundaryStrength = 50f;

            float3 offset = currentPos - center;
            if (math.abs(offset.x) > boundaryThreshold)
                boundaryAvoidance.x = -math.sign(offset.x) * boundaryStrength * (math.abs(offset.x) - boundaryThreshold) / (boundsSize - boundaryThreshold);
            if (math.abs(offset.y) > boundaryThreshold)
                boundaryAvoidance.y = -math.sign(offset.y) * boundaryStrength * (math.abs(offset.y) - boundaryThreshold) / (boundsSize - boundaryThreshold);
            if (math.abs(offset.z) > boundaryThreshold)
                boundaryAvoidance.z = -math.sign(offset.z) * boundaryStrength * (math.abs(offset.z) - boundaryThreshold) / (boundsSize - boundaryThreshold);

            float3 accel = separation * 2f + alignment + cohesion * 0.5f + boundaryAvoidance;
            float3 newVel = currentVel + accel * deltaTime;
            newVel = math.normalize(newVel) * 5f;

            newVelocities[i] = newVel;
        }

        for (int i = 0; i < boidEntities.Length; i++) {
            var transform = boidTransforms[i];
            float3 vel = newVelocities[i];

            transform.Position += vel * deltaTime;

            // Keep boids in bounds
            float3 offset = transform.Position - center;

            if (math.abs(offset.x) > boundsSize) {
                vel.x *= -1f;
                transform.Position.x = math.clamp(transform.Position.x, -boundsSize, boundsSize);
            }

            if (math.abs(offset.y) > boundsSize) {
                vel.y *= -1f;
                transform.Position.y = math.clamp(transform.Position.y, -boundsSize, boundsSize);
            }

            if (math.abs(offset.z) > boundsSize) {
                vel.z *= -1f;
                transform.Position.z = math.clamp(transform.Position.z, -boundsSize, boundsSize);
            }

            transform.Rotation = quaternion.LookRotationSafe(vel, new float3(0, 1, 0));

            state.EntityManager.SetComponentData(boidEntities[i], transform);
            state.EntityManager.SetComponentData(boidEntities[i], new Boid { velocity = vel });
        }

        boidEntities.Dispose();
        boidTransforms.Dispose();
        boidVelocities.Dispose();
        newVelocities.Dispose();
    }
}
