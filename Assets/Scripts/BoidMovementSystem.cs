using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

// This system handles boid movement simulation logic using standard boid rules (separation, alignment, cohesion)
// Runs every frame and updates each boid's velocity and position based on its neighbors and world boundaries

[BurstCompile]                                        // Uses Unity's Burst Compiler to optimize the math-heavy loop operations for performance
[UpdateInGroup(typeof(SimulationSystemGroup))]        // Runs during the Simulation phase of the frame update, after initialization but before rendering
public partial struct BoidMovementSystem : ISystem {

    public void OnUpdate(ref SystemState state) {

        float deltaTime = SystemAPI.Time.DeltaTime;   // Time since last frame, used to keep movement framerate-independent

        float3 center = new float3(0, 0, 0);          // The center of the simulation space
        float boundsSize = 40f;                       // Defines a cube with sides of length 80 units that boids should stay inside

        // Create an entity query to fetch all boids and their transforms
        var entityQuery = SystemAPI.QueryBuilder()
            .WithAll<Boid, LocalTransform>()
            .Build();

        // Extract arrays of data for batch processing
        var boidEntities   = entityQuery.ToEntityArray(Allocator.Temp);                 // List of all boid entities
        var boidTransforms = entityQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);  // Their positions and rotations
        var boidVelocities = entityQuery.ToComponentDataArray<Boid>(Allocator.Temp);    // Their velocities

        var newVelocities = new NativeArray<float3>(boidEntities.Length, Allocator.Temp);  // Temporary array to store new velocities

        for (int i = 0; i < boidEntities.Length; i++) {

            float3 currentPos = boidTransforms[i].Position;
            float3 currentVel = boidVelocities[i].velocity;

            // Boid rule accumulators
            float3 separation = float3.zero;
            float3 alignment = float3.zero;
            float3 cohesion = float3.zero;

            int neighborCount = 0;

            // Inner loop to compare each boid with all others (O(n^2) operation)
            for (int j = 0; j < boidEntities.Length; j++) {

                if (i == j) continue;  // Don't compare with self

                float3 otherPos = boidTransforms[j].Position;
                float3 otherVel = boidVelocities[j].velocity;

                float dist = math.distance(currentPos, otherPos);
                if (dist < 4f) {
                    separation += (currentPos - otherPos) / math.max(dist, 0.01f); // Avoid division by zero
                    alignment += otherVel;
                    cohesion += otherPos;
                    neighborCount++;
                }
            }

            if (neighborCount > 0) {
                separation /= neighborCount;
                alignment  /= neighborCount;
                cohesion    = (cohesion / neighborCount - currentPos);  // Move toward average neighbor position
            }

            // Keep boids within bounds of the simulation space by applying repelling force
            float3 boundaryAvoidance = float3.zero;
            float boundaryThreshold = boundsSize * 0.6f;    // Start pushing back when 60% toward edge
            float boundaryStrength = 50f;                   // Multiplier for how strong the push is

            float3 offset = currentPos - center;
            if (math.abs(offset.x) > boundaryThreshold)
                boundaryAvoidance.x = -math.sign(offset.x) * boundaryStrength * (math.abs(offset.x) - boundaryThreshold) / (boundsSize - boundaryThreshold);
            if (math.abs(offset.y) > boundaryThreshold)
                boundaryAvoidance.y = -math.sign(offset.y) * boundaryStrength * (math.abs(offset.y) - boundaryThreshold) / (boundsSize - boundaryThreshold);
            if (math.abs(offset.z) > boundaryThreshold)
                boundaryAvoidance.z = -math.sign(offset.z) * boundaryStrength * (math.abs(offset.z) - boundaryThreshold) / (boundsSize - boundaryThreshold);

            // Combine all movement rules to compute acceleration
            float3 accel = separation * 2f + alignment + cohesion * 0.5f + boundaryAvoidance;

            // Integrate velocity with acceleration
            float3 newVel = currentVel + accel * deltaTime;
            newVel = math.normalize(newVel) * 5f;    // Normalize and set fixed speed (5 units/sec)

            newVelocities[i] = newVel;
        }

        // Apply the updated velocities and positions to the entities
        for (int i = 0; i < boidEntities.Length; i++) {
            var transform = boidTransforms[i];
            float3 vel = newVelocities[i];

            // Update position using new velocity
            transform.Position += vel * deltaTime;

            // Reflect boid if it hits simulation boundaries
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

            // Rotate the boid to face in the direction of movement
            transform.Rotation = quaternion.LookRotationSafe(vel, new float3(0, 1, 0));

            // Update ECS components with new transform and velocity
            state.EntityManager.SetComponentData(boidEntities[i], transform);
            state.EntityManager.SetComponentData(boidEntities[i], new Boid { velocity = vel });
        }

        // Free up native arrays after use to prevent memory leaks
        boidEntities.Dispose();
        boidTransforms.Dispose();
        boidVelocities.Dispose();
        newVelocities.Dispose();
    }
}