using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class S2Agent_Camera_Find_Items_Deliver : Agent
{
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float rotationSpeed = 360f;

    [SerializeField] private float maxItemSpawnOffset = 1.5f;
    [SerializeField] private float maxWarehouseSpawnOffset = 1.5f;

    [SerializeField] private Rigidbody rigidbody;

    [SerializeField] private Transform colorIndicator;

    [SerializeField] private GameObject body;

    // Rewards
    [SerializeField] private float hitWallPenalty = 0;

    [SerializeField] private float findIncorrectItemReward = 20;

    [SerializeField] private float findCorrectItemReward = 100;

    [SerializeField] private float deliverIncorrectlyReward = 0;

    [SerializeField] private float deliverCorrectlyReward = 100;


    [SerializeField] private float StepTimeReward = 0f;

    // Other Objects

    [SerializeField] private List<GameObject> items;

    [SerializeField] private GameObject warehouse;


    private Material defaultMaterial;

    private GameObject demandedItem;
    private GameObject heldItem = null;


    private float pendingMovement = 0f;
    private float pendingRotation = 0f;

    void Awake()
    {
        // Lock physics to fixed step
        Time.fixedDeltaTime = 1f / 60f;
        Time.maximumDeltaTime = Time.fixedDeltaTime;
        Physics.simulationMode = SimulationMode.Script;
        Physics.autoSyncTransforms = true;
    }

    private void Start()
    {
        defaultMaterial = body.GetComponent<MeshRenderer>().material;
    }

    private void EnterTrigger(Collider other)
    {
        if (items.Contains(other.gameObject))
        {
            if (heldItem == null)
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
                heldItem = other.gameObject;
                other.gameObject.SetActive(false);

                body.GetComponent<MeshRenderer>().material = other.GetComponent<MeshRenderer>().material;
            }
            else
            {
                EndEpisode();
            }
            
        }
        else if (other.gameObject == warehouse)
        {
            if (heldItem == demandedItem)
            {
                Debug.Log("Delivered Correctly");
                AddReward(deliverCorrectlyReward);
            }
            else
            {
                Debug.Log("Delivered Incorrectly");
                AddReward(deliverIncorrectlyReward);
            }
            EndEpisode();
        }
    }

    private void SetupSimulation()
    {
        // Reset agent's state and position
        heldItem = null;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), transform.up);
        
        // Randomize items positions and set demanded item
        float randomItemId = Random.Range(0, items.Count);

        int i = 0;
        foreach (GameObject item in items)
        {
            item.transform.localPosition = new Vector3(Random.Range(-maxItemSpawnOffset, maxItemSpawnOffset), 0, Random.Range(-maxItemSpawnOffset, maxItemSpawnOffset));
            item.SetActive(true);
            if (i == randomItemId)
            {
                demandedItem = item;
            }

            i++;
        }

        // Randomize Warehouse position
        warehouse.transform.localPosition = new Vector3(Random.Range(-maxWarehouseSpawnOffset, maxWarehouseSpawnOffset), 0, Random.Range(-maxWarehouseSpawnOffset, maxWarehouseSpawnOffset));

        // Update colors of indicator and agent's body
        colorIndicator.GetComponent<MeshRenderer>().material = demandedItem.GetComponent<MeshRenderer>().material;
        body.GetComponent<MeshRenderer>().material = defaultMaterial;
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        // Read base seed from EnvironmentParameters (set in Python via EnvironmentParametersChannel)
        float baseSeedParam = Academy.Instance.EnvironmentParameters.GetWithDefault("seed", 0f);
        int seed = Mathf.FloorToInt(baseSeedParam) + CompletedEpisodes;

        Random.InitState(seed); // Not the same as System.Random

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

        // It's important that the items are always passed in the same order to the network
        int demandedItemIndex = items.FindIndex(el => el == demandedItem);
        int heldItemIndex = items.FindIndex(el => el == heldItem);

        // Item indices
        sensor.AddObservation(demandedItemIndex + 1);
        sensor.AddObservation(heldItemIndex + 1);

        // Remember to handle position on the map separately - in gymnasium wrapper
        float posX = transform.position.x;
        float posZ = transform.position.z;

        sensor.AddObservation(posX);
        sensor.AddObservation(posZ);

        // Rotation described as forward - can be displayed as a heatmap of directions
        float forwardX = transform.forward.x;
        float forwardZ = transform.forward.z;

        sensor.AddObservation(forwardX);
        sensor.AddObservation(forwardZ);
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

        pendingMovement = movement * Time.fixedDeltaTime * this.movementSpeed;
        pendingRotation = rotation * Time.fixedDeltaTime * this.rotationSpeed;

        if (StepCount >= MaxStep)
        {
            Debug.Log("Timeout - reward - " + GetCumulativeReward());
            AddReward(MaxStep * StepTimeReward);
        }

        AddReward(StepTimeReward);

        Physics.Simulate(Time.fixedDeltaTime);
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
