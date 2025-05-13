using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class AgentS2Camera : Agent
{
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float rotationSpeed = 180f;

    [SerializeField] private int numberOfRays = 8; // Number of rays to cast
    [SerializeField] private float rayDistance = 2.5f; // Distance of each ray
    [SerializeField] private float rayMissedValue = 100f; // Value to use if ray doesn't hit anything

    [SerializeField] private Transform warehouseTransform;


    [SerializeField] private float maxSpawnOffset = 2f;

    [SerializeField] private Rigidbody rigidbody;

    // Rewards
    [SerializeField] private float hitWallPenalty = -50;

    [SerializeField] private float FindWarehouseReward = 50;

    [SerializeField] private float StepTimeReward = -0.2f;

    // Potential Based Reward
    [SerializeField] private float potentialRewardDistanceCoefficient = 0.1f;

    private const float UNDEFINED_POTENTIAL = float.MinValue;
    private float potentialMeasure = UNDEFINED_POTENTIAL;

    private float potentialRewardsCum = 0;

    private void EnterTrigger(Collider other)
    {
        if (other.tag == "Warehouse")
        {
            Debug.Log("Dropped Correctly");
            heldResource = Resource.None;
            AddReward(FindWarehouseReward);

            EndEpisode();
        }
    }

    private Resource heldResource = Resource.None;
    private Resource demandedResource;


    private void SetupSimulation()
    {
        potentialRewardsCum = 0;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), transform.up);

        // Randomize warehouse locations
        warehouseTransform.localPosition = new Vector3(Random.Range(-maxSpawnOffset, maxSpawnOffset), 0, Random.Range(-maxSpawnOffset, maxSpawnOffset));

    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        SetupSimulation();
    }

    private float[] GetRaycastObservations()
    {
        float[] results = new float[numberOfRays];

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
                results[i] = hit.distance;
                // Log the hit position
                // Debug.Log($"Ray {i} hit: {hit.point}");

                // Visualize the ray in the Scene view
                Debug.DrawLine(startPosition, hit.point, Color.green);
            }
            else
            {
                results[i] = rayMissedValue;

                // Visualize the ray in the Scene view (no hit)
                Debug.DrawLine(startPosition, startPosition + direction * rayDistance, Color.red);
            }
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

        float[] raycastsObservations = GetRaycastObservations();
        sensor.AddObservation(raycastsObservations);
    }

    /**
     * Calculates potential measure and adds reward based on previous one
     * Adds potential based reward/penalty - potential being the negative distance
     */
    private void ManageDistanceReward()
    {
        float newPotentialMeasure = -(transform.position - warehouseTransform.position).magnitude * potentialRewardDistanceCoefficient;
        if (potentialMeasure != UNDEFINED_POTENTIAL)
        {
            float rew = newPotentialMeasure - potentialMeasure;
            //Debug.Log("distance reward = " + rew);
            AddReward(rew);
            potentialRewardsCum += rew;
        }
        potentialMeasure = newPotentialMeasure;
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
        ManageDistanceReward();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Wall")
        {
            AddReward(hitWallPenalty);

            //Debug.Log("Cumulative potential reward = " + potentialRewardsCum);
            //Debug.Log("Avg potential reward = " + potentialRewardsCum / StepCount);
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
