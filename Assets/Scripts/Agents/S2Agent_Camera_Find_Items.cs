using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class S2Agent_Camera_Find_Items : Agent
{
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float rotationSpeed = 360f;

    [SerializeField] private float maxSpawnOffset = 1.5f;

    [SerializeField] private Rigidbody rigidbody;

    [SerializeField] private Transform colorIndicator;

    // Rewards
    [SerializeField] private float hitWallPenalty = 0;

    [SerializeField] private float findIncorrectItemReward = 20;

    [SerializeField] private float findCorrectItemReward = 100;

    [SerializeField] private float StepTimeReward = 0f;

    [SerializeField] private List<GameObject> items;

    private GameObject demandedItem;

    private int baseSeed = 0;

    private float pendingMovement = 0f;
    private float pendingRotation = 0f;

    private void EnterTrigger(Collider other)
    {
        if (items.Contains(other.gameObject))
        {
            if (other.gameObject == demandedItem)
            {
                Debug.Log("Picked up Correctly");
                AddReward(findCorrectItemReward);
            }
            else
            {
                Debug.Log("Picked up Incorrectly");
                AddReward(findIncorrectItemReward);
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

        int seed = baseSeed + CompletedEpisodes;
        Random.InitState(seed);

        SetupSimulation();
    }

    private struct RaycastObservation
    {
        public float distance;
        public bool[] items;
    };

    public override void CollectObservations(VectorSensor sensor)
    {
        // possible item states: [None, item1, item2, ...]
        int demandedItemIndex = items.FindIndex(el => el == demandedItem);
        // Item indices

        // Index 0 reserved for None
        sensor.AddObservation(demandedItemIndex+1);
        sensor.AddObservation(0);
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

        /*movement = movement * Time.fixedDeltaTime * this.movementSpeed;
        rotation = rotation * Time.fixedDeltaTime * this.rotationSpeed;

        transform.localPosition += transform.forward * movement;
        transform.Rotate(new Vector3(0, rotation, 0));*/

        pendingMovement = movement * Time.fixedDeltaTime * this.movementSpeed;
        pendingRotation = rotation * Time.fixedDeltaTime * this.rotationSpeed;

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

        if (pendingMovement != 0f)
        {
            rigidbody.MovePosition(rigidbody.position + transform.forward * pendingMovement);
            pendingMovement = 0f;
        }

        if (pendingRotation != 0f)
        {
            rigidbody.MoveRotation(rigidbody.rotation * Quaternion.Euler(0, pendingRotation, 0));
            pendingRotation = 0f;
        }
    }
}
