using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;
using System.Collections.Generic;

public class RobotAgentSimple : Agent
{
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float rotationSpeed = 180f;

    [SerializeField] private int numberOfRays = 8; // Number of rays to cast
    [SerializeField] private float rayDistance = 10f; // Distance of each ray

    [SerializeField] private Transform warehouseTransform;

    [SerializeField] private Transform colorIndicator;



    [SerializeField] private float maxSpawnOffset = 2f;

    [SerializeField] private Renderer body;



    [SerializeField] private Rigidbody rigidbody;

    // Rewards
    [SerializeField] private float hitWallPenalty = -50;

    [SerializeField] private float PickupCorrectReward = 100;
    [SerializeField] private float PickupIncorrectPenalty = -100;

    [SerializeField] private float DropCorrectReward = 100;

    [SerializeField] private float StepTimeReward = -0.4f;

    [SerializeField] private Material neutralMaterial;
    [SerializeField] private List<GameObject> items;


    private Resource TagToResource(string tag)
    {
        if (tag == "Resource_Blue") return Resource.Blue;
        if (tag == "Resource_Yellow") return Resource.Yellow;

        return Resource.None;
    }

    private void EnterTrigger(Collider other)
    {
        if (other.tag == "Warehouse")
        {
            if (heldResource == demandedResource)
            {
                Debug.Log("Dropped Correctly");
                heldResource = Resource.None;
                AddReward(DropCorrectReward);

                EndEpisode();
            }
        }
        else
        {
            foreach (GameObject item in items)
            {
                if (other.tag == item.tag)
                {
                    // Found entered item

                    if (TagToResource(item.tag) == demandedResource && demandedResource != heldResource)
                    {
                        // pick up
                        Debug.Log("Picked up correctly");
                        heldResource = demandedResource;
                        AddReward(PickupCorrectReward);

                        SetBodyColor();
                        item.SetActive(false);
                    }
                    else
                    {
                        Debug.Log("Picked up incorrectly");
                        AddReward(PickupIncorrectPenalty);
                    }
                }
            }
        }
    }

    private Resource heldResource = Resource.None;
    private Resource demandedResource;

    private Material GetProperColor()
    {
        foreach (GameObject item in items)
        {
            if (TagToResource(item.tag) == heldResource)
            {
                Material material = item.gameObject.GetComponent<Renderer>().material;
                return material;
            }
        }

        return neutralMaterial;
    }
    private void SetBodyColor()
    {
        body.material = GetProperColor();
    }

    private void ChooseDemandedResource()
    {
        int id = Random.Range(0, items.Count);
        demandedResource = TagToResource(items[id].tag);
    }

    private void SetupSimulation()
    {
        // Activate resources again

        foreach (GameObject item in items)
        {
            item.SetActive(true);
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), transform.up);

        // Randomize strategic points locations
        warehouseTransform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));

        foreach (GameObject item in items)
        {
            item.transform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));
        }

        // Randomize Initial state
        float random = Random.Range(0f, 1f);
        ChooseDemandedResource();

        // Set indicator to demanded resource
        foreach (GameObject item in items)
        {
            if (TagToResource(item.tag) == demandedResource)
            {
                Material material = item.gameObject.GetComponent<Renderer>().material;

                colorIndicator.GetComponent<Renderer>().material = material;
            }
        }

       
        if (random < 0.5)
        {
            heldResource = demandedResource;

            // Deactivate already picked up resource
            foreach (GameObject item in items)
            {
                if (TagToResource(item.tag) == heldResource)
                {
                    item.SetActive(false);
                }
            }
        }
        else
        {
            heldResource = Resource.None;
        }

        SetBodyColor();
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        SetupSimulation();
    }

    private struct RaycastObservation
    {
        public float distance;
        public bool warehousHit;
        public bool yellowResourceHit;
        public bool blueResourceHit;
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

            // Debuging Lines
            if (blocked)
            {
                // Log the hit position
                // Debug.Log($"Ray {i} hit: {hit.point}");

                // Visualize the ray in the Scene view
                Debug.DrawLine(startPosition, hit.point, Color.green);
            }
            else
            {
                // Visualize the ray in the Scene view (no hit)
                Debug.DrawLine(startPosition, startPosition + direction * rayDistance, Color.red);
            }

            // Adding observations

            RaycastObservation raycastObservation = new RaycastObservation();
            tag = hit.collider.tag;
            raycastObservation.distance = hit.distance;
            raycastObservation.warehousHit = tag == "Warehouse";
            raycastObservation.yellowResourceHit = tag == "Resource_Yellow";
            raycastObservation.blueResourceHit = tag == "Resource_Blue";

            results[i] = raycastObservation;
        }

        return results;
    }


    public override void CollectObservations(VectorSensor sensor)
    {

        // Demanded Resource
        bool isBlueDemanded = demandedResource == Resource.Blue;
        sensor.AddObservation(isBlueDemanded);

        bool isYellowDemanded = demandedResource == Resource.Yellow;
        sensor.AddObservation(isYellowDemanded);

        // Held Resource
        bool isBlueHeld = heldResource == Resource.Blue;
        sensor.AddObservation(isBlueHeld);

        bool isYellowHeld = heldResource == Resource.Blue;
        sensor.AddObservation(isYellowHeld);


        // Raycasts - Lines of Sight

        RaycastObservation[] raycastsObservations = GetRaycastObservations();

        foreach (RaycastObservation obs in raycastsObservations)
        {
            sensor.AddObservation(obs.distance);
            sensor.AddObservation(obs.warehousHit);
            sensor.AddObservation(obs.yellowResourceHit);
            sensor.AddObservation(obs.blueResourceHit);
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
        }

        AddReward(StepTimeReward);

        /*if (action == 0)
        {
            AddReward(StepMovementReward);
        }
        else
        {
            AddReward(StepTimeReward);
        }*/
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Wall")
        {
            //Debug.Log("Hit Wall");
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
