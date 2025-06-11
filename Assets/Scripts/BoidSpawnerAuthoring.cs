using Unity.Collections;
using Unity.Entities;
using UnityEngine;

// This is the part of the script that is visible in Unity
public class BoidSpawnerAuthoring : MonoBehaviour {
    public GameObject[] prefabs;                    // Array of boid prefabs to use
    public int[] counts;                            // Count of each boid prefab. Uses the same index as the boids for count so
                                                    // boid 0 will have counts[0] amount of boids created
}

// This struct defines the data for each boid spawner in ECS
public struct BoidSpawner : IComponentData {
    public Entity prefab;                           // The entity to instantiate as a boid
    public int count;                               // How many instances of entity prefab to spawn
}

// This baker converts the authoring class to a ECS component during the conversion process.
// For each prefab and count pair, it creates a separate ECS entity with a BoidSpawner component.
public class BoidSpawnerBaker : Baker<BoidSpawnerAuthoring> {

    public override void Bake(BoidSpawnerAuthoring authoring) {

        // Loop through all prefabs in the authoring component
        for (int i = 0; i < authoring.prefabs.Length; i++) {

            var entity = CreateAdditionalEntity(TransformUsageFlags.None);                          // Creates the new entity

            var prefabEntity = GetEntity(authoring.prefabs[i], TransformUsageFlags.Dynamic);        // Convert the GameObject prefab to an ECS entity prefab
                                                                                                    // ECS entity = GameObject prefab but it's for an entity

            // Add the BoidSpawner component to the new entity with the prefab reference and spawn count
            AddComponent(entity, new BoidSpawner {
                prefab = prefabEntity,
                count = authoring.counts[i]
            });
        }
    }
}