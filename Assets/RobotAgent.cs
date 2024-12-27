using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RobotAgent : Agent
{
    [SerializeField] private float movementSpeed = 3f;
    [SerializeField] private float rotationSpeed = 180f;

    //[SerializeField] private Transform warehouseTransform;
    private bool isWithinWarehouse = false;

    [SerializeField] private Transform resourceBlueTransform;
    private bool isNearBlueResource = false;

    [SerializeField] private Transform resourceYellowTransform;
    private bool isNearYellowResource = false;

    /*// Picking up colors
    [SerializeField] private float DropIncorrectPenalty = -100;*/
    private Material originalMaterial;
    [SerializeField] private Renderer body;



    [SerializeField] private Rigidbody rigidbody;

    // Rewards
    [SerializeField] private float hitWallPenalty = -1;

    [SerializeField] private float PickupCorrectReward = 100;
    [SerializeField] private float PickupIncorrectPenalty = -100;

    [SerializeField] private float DropCorrectReward = 100;
    [SerializeField] private float DropIncorrectPenalty = -100;

    private Resource heldResource = Resource.None;
    private Resource demandedResource;

    private void Start()
    {
        originalMaterial = body.material;
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

    private void DropAction()
    {

        if (heldResource == Resource.Blue && isWithinWarehouse && demandedResource == Resource.Blue)
        {
            Debug.Log("Dropped Correctly blue");
            heldResource = Resource.None;
            AddReward(DropCorrectReward);
            ChooseDemandedResource();

            body.material = originalMaterial;
        }
        else if (heldResource == Resource.Yellow && isWithinWarehouse && demandedResource == Resource.Yellow)
        {
            Debug.Log("Dropped Correctly yellow");
            heldResource = Resource.None;
            AddReward(DropCorrectReward);
            ChooseDemandedResource();

            body.material = originalMaterial;
        }
        else
        {
            AddReward(DropIncorrectPenalty);
        }
    }

    private void PickupAction()
    {
        if (heldResource == Resource.None && isNearBlueResource && demandedResource == Resource.Blue)
        {
            Debug.Log("PickedUp Correctly blue");
            heldResource = demandedResource;
            AddReward(PickupCorrectReward);

            Material material = resourceBlueTransform.GetComponent<Renderer>().material;

            body.material = material;
        }
        else if (heldResource == Resource.None && isNearYellowResource && demandedResource == Resource.Yellow)
        {
            Debug.Log("PickedUp Correctly yellow");
            heldResource = demandedResource;
            AddReward(PickupCorrectReward);

            Material material = resourceYellowTransform.GetComponent<Renderer>().material;

            body.material = material;
        }
        else
        {
            AddReward(PickupIncorrectPenalty);
        }
    }

    private void SetupSimulation()
    {
        transform.localPosition = Vector3.zero;
        heldResource = Resource.None;
        ChooseDemandedResource();

        body.material = originalMaterial;
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        SetupSimulation();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 robotPosition = transform.localPosition;
        Vector3 robotOrientation = transform.forward;

        sensor.AddObservation(new Vector2(robotPosition.x, robotPosition.z));
        sensor.AddObservation(new Vector2(robotOrientation.x, robotOrientation.z));

        // Demanded Resource
        int isBlueDemanded = 0;
        if (demandedResource == Resource.Blue) isBlueDemanded = 1;
        sensor.AddObservation(isBlueDemanded);

        int isYellowDemanded = 0;
        if (demandedResource == Resource.Yellow) isYellowDemanded = 1;
        sensor.AddObservation(isYellowDemanded);

        // Held Resource
        int isBlueHeld = 0;
        if (heldResource == Resource.Blue) isBlueHeld = 1;
        sensor.AddObservation(isBlueHeld);

        int isYellowHeld = 0;
        if (heldResource == Resource.Blue) isYellowHeld = 1;
        sensor.AddObservation(isYellowHeld);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        float rotation = 0;
        float movement = 0;

        // stay
        // move forward
        // rotate left
        // rotate right

        if (action == 0)
        {
            movement = 0;
        }
        else if (action == 1)
        {
            movement = 1;
        }
        else if (action == 2)
        {
            rotation = -1;
        }
        else if (action == 3)
        {
            rotation = 1;
        }
        else if (action == 4)
        {
            PickupAction();
        }
        else if (action == 5)
        {
            DropAction();
        }

        movement = movement * Time.fixedDeltaTime * this.movementSpeed;
        rotation = rotation * Time.fixedDeltaTime * this.rotationSpeed;

        var newPosition = transform.position + transform.forward * movement;
        var newRotation = Quaternion.Euler(0, rotation, 0) * rigidbody.rotation;

        rigidbody.Move(newPosition, newRotation);

        if (StepCount >= MaxStep)
        {
            Debug.Log("Timeout - reward - " + GetCumulativeReward());
        }
    }

    /*public override void Heuristic(in ActionBuffers actionsOut)
    {

    }*/

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Wall")
        {
            //Debug.Log("Hit Wall");
            AddReward(hitWallPenalty);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Resource_Blue")
        {
            isNearBlueResource = true;
        }
        else if (other.tag == "Resource_Yellow")
        {
            isNearYellowResource = true;
        }
        else if (other.tag == "Warehouse")
        {
            isWithinWarehouse = true;
        }
        
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Resource_Blue")
        {
            isNearBlueResource = false;
        }
        else if (other.tag == "Resource_Yellow")
        {
            isNearYellowResource = false;
        }
        else if (other.tag == "Warehouse")
        {
            isWithinWarehouse = false;
        }
    }

    private void Update()
    {

    }
}
