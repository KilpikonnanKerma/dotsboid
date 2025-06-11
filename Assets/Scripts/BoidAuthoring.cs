using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

// Empty MonoBehaviour since the boid itself doesn't need anything from Unity Editor
// Must be kept since this is what is applied to the boid prefab
public class BoidAuthoring : MonoBehaviour { }

// ECS entity data
public struct Boid : IComponentData {
    public float3 velocity;                 // The direction and speed the boid is moving
}

// Baker class responsible for converting the BoidAuthoring GameObject into an ECS entity with components.
public class BoidBaker : Baker<BoidAuthoring> {

    public override void Bake(BoidAuthoring authoring) {

        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // Add the Boid component and initialize with a random velocity vector
        AddComponent(entity, new Boid {
            velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 3f)
        });

        // Add a LocalTransform component to set the entity's initial position, rotation, and scale.
        AddComponent(entity, new LocalTransform {
            Position = float3.zero,
            Rotation = quaternion.EulerXYZ(
                UnityEngine.Random.Range(0, 360),
                UnityEngine.Random.Range(0, 360),
                UnityEngine.Random.Range(0, 360)
            ),
            Scale = 1f
        });
    }
}