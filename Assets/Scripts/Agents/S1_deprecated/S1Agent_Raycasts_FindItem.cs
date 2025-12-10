using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;
using System.Collections.Generic;

public class S1Agent_Raycasts_FindItem : Agent
{
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float rotationSpeed = 360f;

    [SerializeField] private int numberOfRays = 16; // Number of rays to cast
    [SerializeField] private float rayDistance = 100f; // Distance of each ray

    [SerializeField] private float maxSpawnOffset = 1.5f;

    [SerializeField] private Rigidbody rigidbody;

    [SerializeField] private Transform colorIndicator;

    // Rewards
    [SerializeField] private float hitWallPenalty = 0;

    [SerializeField] private float findItemReward = 100;

    [SerializeField] private float StepTimeReward = 0f;

    [SerializeField] private List<GameObject> items;

    private GameObject demandedItem;

    private void EnterTrigger(Collider other)
    {
        if (items.Contains(other.gameObject))
        {
            if (other.gameObject == demandedItem)
            {
                Debug.Log("Picked up Correctly");
                AddReward(findItemReward);
            }
            else
            {
                Debug.Log("Picked up Incorrectly");
            }
            

            EndEpisode();
        }
    }

    private void SetupSimulation()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), transform.up);

        float randomItemId = Random.Range(0, items.Count);

        // Randomize strategic points locations and set demanded item
        int i = 0;
        foreach (GameObject item in items)
        {
            item.transform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));
            if (i == randomItemId)
            {
                demandedItem = item;
            }

            i++;
        }

        // Update Color of Indicator
        colorIndicator.GetComponent<MeshRenderer>().material = demandedItem.GetComponent<MeshRenderer>().material;
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        SetupSimulation();
    }

    private struct RaycastObservation
    {
        public float distance;
        public bool[] items;
    };

    private RaycastObservation[] GetRaycastObservations()
    {
        RaycastObservation[] results = new RaycastObservation[numberOfRays];

        float angleIncrement = 360f / numberOfRays;

        for (int i = 0; i < numberOfRays; i++)
        {
            // Calculate the direction of the ray
            float angle = i * angleIncrement;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            Vector3 startPosition = transform.position;
            startPosition.y = 0.5f;

            // Perform the raycast
            bool blocked = Physics.Raycast(startPosition, direction, out RaycastHit hit, rayDistance);

            // Adding observations
            RaycastObservation raycastObservation = new RaycastObservation();
            raycastObservation.items = new bool[2];
            // Debuging Lines
            if (blocked)
            {
                // Visualize the ray in the Scene view
                Debug.DrawLine(startPosition, hit.point, Color.green);

                int j = 0;
                foreach (GameObject item in items)
                {
                    raycastObservation.items[j] = false;
                    if (item == hit.collider.gameObject)
                    {
                        raycastObservation.items[j] = true;
                        Debug.DrawLine(startPosition, hit.point, Color.cyan);
                    }

                    j++;
                }
            }
            else
            {
                // Visualize the ray in the Scene view (no hit)
                Debug.DrawLine(startPosition, startPosition + direction * rayDistance, Color.red);
            }


            tag = hit.collider.tag;
            raycastObservation.distance = hit.distance;

            results[i] = raycastObservation;
        }

        return results;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Adds oneHotEncoded demanded items
        int demandedItemIndex = items.FindIndex(el => el == demandedItem);
        for (int i = 0; i < items.Count; i++)
        {
            sensor.AddObservation(i == demandedItemIndex);
        }

        // Raycasts - Lines of Sight

        RaycastObservation[] raycastsObservations = GetRaycastObservations();

        foreach (RaycastObservation obs in raycastsObservations)
        {
            sensor.AddObservation(obs.distance);
            foreach (bool item in obs.items)
            {
                sensor.AddObservation(item);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        float rotation = 0;
        float movement = 0;

        if (action == 0)
        {
            movement = 1;
        }
        else if (action == 1)
        {
            rotation = -1;
        }
        else if (action == 2)
        {
            rotation = 1;
        }

        movement = movement * Time.fixedDeltaTime * this.movementSpeed;
        rotation = rotation * Time.fixedDeltaTime * this.rotationSpeed;

        transform.localPosition += transform.forward * movement;
        transform.Rotate(new Vector3(0, rotation, 0));

        if (StepCount >= MaxStep)
        {
            Debug.Log("Timeout - reward - " + GetCumulativeReward());
            AddReward(MaxStep * StepTimeReward);
        }

        AddReward(StepTimeReward);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Wall")
        {
            AddReward(hitWallPenalty);

            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EnterTrigger(other);
    }

    private void FixedUpdate()
    {
        rigidbody.rotation = Quaternion.Euler(0, rigidbody.rotation.eulerAngles.y, 0);
    }
}
