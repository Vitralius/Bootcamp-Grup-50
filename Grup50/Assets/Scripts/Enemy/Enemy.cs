using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private GameObject patrolPoints;
    [SerializeField] private NavMeshAgent navMeshAgent;
    private GameObject[] points;
    private int counter;
    private int targetPoint;
    private bool isArrived;
    void Start()
    {
        isArrived = true;
        counter = patrolPoints.transform.childCount;
        points = new GameObject[counter];
        SetPatrolPoints();
        targetPoint = NextPoint();
    }
    // Update is called once per frame
    void Update()
    {
        if (isArrived)
        {
            navMeshAgent.isStopped = true;
            points[targetPoint].SetActive(false);
            targetPoint = NextPoint();
            points[targetPoint].SetActive(true);
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(points[targetPoint].transform.position);
            isArrived = false;
        }
        else
        {
            isArrived = CheckDistance();
        }

    }
    public void SetPatrolPoints()
    {
        for (int i = 0; i < counter; i++)
        {
            points[i] = patrolPoints.transform.GetChild(i).gameObject;
        }
    }
    public int NextPoint() { return Random.Range(0, counter); }
    public bool CheckDistance() { return Vector3.Distance(this.transform.position, points[targetPoint].transform.position) < 1f; }
}
