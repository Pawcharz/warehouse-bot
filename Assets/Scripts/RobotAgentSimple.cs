using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;

public class RobotAgentSimple : Agent
{
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float rotationSpeed = 180f;

    [SerializeField] private int numberOfRays = 8; // Number of rays to cast
    [SerializeField] private float rayDistance = 10f; // Distance of each ray

    [SerializeField] private Transform warehouseTransform;

    [SerializeField] private Transform resourceBlueTransform;

    [SerializeField] private Transform resourceYellowTransform;

    [SerializeField] private float maxSpawnOffset = 2f;

    [SerializeField] private Renderer body;



    [SerializeField] private Rigidbody rigidbody;

    // Rewards
    [SerializeField] private float hitWallPenalty = -1;

    [SerializeField] private float PickupCorrectReward = 100;
    [SerializeField] private float PickupIncorrectPenalty = -100;

    [SerializeField] private float DropCorrectReward = 100;
    [SerializeField] private float DropIncorrectReward = -100;

    [SerializeField] private float StepReward = -0.2f;

    [SerializeField] private Material neutralMaterial;

    private Resource heldResource = Resource.None;
    private Resource demandedResource;

    private void SetBodyColor()
    {
        if (heldResource == Resource.Yellow)
        {
            Material material = resourceYellowTransform.GetComponent<Renderer>().material;
            body.material = material;
        }
        else if (heldResource == Resource.Blue)
        {
            Material material = resourceBlueTransform.GetComponent<Renderer>().material;
            body.material = material;
        }
        else
        {
            body.material = neutralMaterial;
        }
    }

    private void ChooseDemandedResource()
    {
        int drawn = Random.Range(0, 2);

        if (drawn == 0)
        {
            demandedResource = Resource.Blue;
        }
        else
        {
            demandedResource = Resource.Yellow;
        }
    }

    private void SetupSimulation()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), transform.up);

        // Randomize strategic points locations
        warehouseTransform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));
        resourceYellowTransform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));
        resourceBlueTransform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));

        // Randomize Initial state
        float random = Random.Range(0f, 1f);
        if (random < 0.3)
        {
            demandedResource = Resource.Yellow;
            heldResource = Resource.Yellow;
        }
        else if (random < 0.6)
        {
            demandedResource = Resource.Blue;
            heldResource = Resource.Blue;
        }
        else
        {
            ChooseDemandedResource();
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
        /*Vector3 robotPosition = transform.localPosition;
        Vector3 robotOrientation = transform.forward;

        sensor.AddObservation(new Vector2(robotPosition.x, robotPosition.z));
        sensor.AddObservation(new Vector2(robotOrientation.x, robotOrientation.z));*/

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
        float movement = 1;

        /*float movement = 0;

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
        }*/

        if (action == 0)
        {
            rotation = -1;
        }
        else if (action == 1)
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

        AddReward(StepReward);
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
        if (other.tag == "Resource_Blue")
        {
            if (heldResource == Resource.None && demandedResource == Resource.Blue)
            {
                Debug.Log("PickedUp Correctly blue");
                heldResource = demandedResource;
                AddReward(PickupCorrectReward);

                SetBodyColor();
            }
            else
            {
                Debug.Log("PickedUp Incorrectly blue");
                AddReward(PickupIncorrectPenalty);
            }
            
        }
        else if (other.tag == "Resource_Yellow")
        {
            if (heldResource == Resource.None && demandedResource == Resource.Yellow)
            {
                Debug.Log("PickedUp Correctly yellow");
                heldResource = demandedResource;
                AddReward(PickupCorrectReward);

                SetBodyColor();
            }
            else
            {
                Debug.Log("PickedUp Incorrectly yellow");
                AddReward(PickupIncorrectPenalty);
            }
        }
        else if (other.tag == "Warehouse")
        {
            if (heldResource == demandedResource)
            {
                Debug.Log("Dropped Correctly");
                heldResource = Resource.None;
                AddReward(DropCorrectReward);

                EndEpisode();
                //ChooseDemandedResource();

                // body.material = neutralMaterial;
            }
            else
            {
                Debug.Log("Dropped Inorrectly");
                AddReward(DropIncorrectReward);
            }
        }
        
    }

    private void FixedUpdate()
    {
        rigidbody.rotation = Quaternion.Euler(0, rigidbody.rotation.eulerAngles.y, 0);
    }
}
