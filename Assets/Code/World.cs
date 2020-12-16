﻿/// ---------------------------------------------
/// Contact: Henry Braun
/// Brief: Defines the world environment
/// Thanks to VHLab for original implementation
/// Date: November 2017 
/// ---------------------------------------------

using UnityEngine;
using System.Collections.Generic;
using UnityEditor.AI;
using System.Collections;
using UnityEngine.AI;
using System.Linq;

namespace Biocrowds.Core
{
    public class World : MonoBehaviour
    {
        //agent radius
        private const float AGENT_RADIUS = 1.00f;

        //radius for auxin collide
        private const float AUXIN_RADIUS = 0.1f;

        //density
        private const float AUXIN_DENSITY = 0.50f;//0.45f; //0.65f;

        private const float GOAL_DISTANCE_THRESHOLD = 0.1f;

        [SerializeField]
        private Terrain _terrain;

        [SerializeField]
        private Transform _goal;

        [SerializeField]
        private Vector2 _dimension = new Vector2(30.0f, 20.0f);
        public Vector2 Dimension
        {
            get { return _dimension; }
        }

        //number of agents in the scene
        [SerializeField]
        private int _maxAgents = 30;

        //agent prefab
        [SerializeField]
        private Agent _agentPrefab;

        [SerializeField]
        private List<Agent> _agentPrefabList;

        [SerializeField]
        private Cell _cellPrefab;

        [SerializeField]
        private Auxin _auxinPrefab;

        [SerializeField]
        private BoxCollider _obstacleCollider;

        [SerializeField]
        private List<Agent> _agents = new List<Agent>();
        List<Cell> _cells = new List<Cell>();
        List<Auxin> _auxins = new List<Auxin>();

        public List<SpawnArea> spawnAreas;

        [SerializeField]
        private Transform _agentsContainer;
        private int _newAgentID = 0;
        public bool removeAtFinalGoal = true;

        public List<Cell> Cells
        {
            get { return _cells; }
        }

        public List<Auxin> Auxins
        {
            get { return _auxins; }
        }


        //max auxins on the ground
        private int _maxAuxins;
        private bool _isReady;

        private void Awake()
        {
            _newAgentID = 0;
            if (spawnAreas.Count == 0)
                spawnAreas = FindObjectsOfType<SpawnArea>().ToList();
        }

        public void LoadWorld()
        {
            StartCoroutine(SetupWorld());
        }

        // Use this for initialization
        IEnumerator SetupWorld()
        {
            //Application.runInBackground = true;

            //change terrain size according informed
            _terrain.terrainData.size = new Vector3(_dimension.x, _terrain.terrainData.size.y, _dimension.y);

            //create all cells based on dimension
            yield return StartCoroutine(CreateCells());

            //populate cells with auxins
            yield return StartCoroutine(DartThrowing());

            //create our agents
            yield return StartCoroutine(CreateAgents());

            //build the navmesh at runtime
            //NavMeshBuilder.BuildNavMesh();

            //wait a little bit to start moving
            yield return new WaitForSeconds(1.0f);
            _isReady = true;
        }

        private IEnumerator CreateCells()
        {
            Transform cellPool = new GameObject("Cells").transform;

            for (int i = 0; i < _dimension.x / 2; i++) //i + agentRadius * 2
            {
                for (int j = 0; j < _dimension.y / 2; j++) // j + agentRadius * 2
                {
                    //instantiante a new cell
                    Cell newCell = Instantiate(_cellPrefab, new Vector3(1.0f + (i * 2.0f), 0.0f, 1.0f + (j * 2.0f)), Quaternion.Euler(90.0f, 0.0f, 0.0f), cellPool);

                    //change its name
                    newCell.name = "Cell [" + i + "][" + j + "]";

                    //metadata for optimization
                    newCell.X = i;
                    newCell.Z = j;

                    newCell.ShowMesh(SceneController.ShowCells);

                    _cells.Add(newCell);

                    yield return null;
                }
            }
        }

        private IEnumerator DartThrowing()
        {
            //lets set the qntAuxins for each cell according the density estimation
            float densityToQnt = AUXIN_DENSITY;

            Transform auxinPool = new GameObject("Auxins").transform;

            densityToQnt *= 2f / (2.0f * AUXIN_RADIUS);
            densityToQnt *= 2f / (2.0f * AUXIN_RADIUS);

            _maxAuxins = (int)Mathf.Floor(densityToQnt);

            //for each cell, we generate its auxins
            for (int c = 0; c < _cells.Count; c++)
            {
                //Dart throwing auxins
                //use this flag to break the loop if it is taking too long (maybe there is no more space)
                int flag = 0;
                for (int i = 0; i < _maxAuxins; i++)
                {
                    float x = Random.Range(_cells[c].transform.position.x - 0.99f, _cells[c].transform.position.x + 0.99f);
                    float z = Random.Range(_cells[c].transform.position.z - 0.99f, _cells[c].transform.position.z + 0.99f);

                    //see if there are auxins in this radius. if not, instantiante
                    List<Auxin> allAuxinsInCell = _cells[c].Auxins;
                    bool createAuxin = true;
                    for (int j = 0; j < allAuxinsInCell.Count; j++)
                    {
                        float distanceAASqr = (new Vector3(x, 0f, z) - allAuxinsInCell[j].Position).sqrMagnitude;

                        //if it is too near no need to add another
                        if (distanceAASqr < AUXIN_RADIUS * AUXIN_RADIUS)
                        {
                            createAuxin = false;
                            break;
                        }
                    }

                    //if i have found no auxin, i still need to check if is there obstacles on the way
                    if (createAuxin)
                    {
                        //sphere collider to try to find the obstacles
                        //NavMeshHit hit;
                        //createAuxin = NavMesh.Raycast(new Vector3(x, 2f, z), new Vector3(x, -2f, z), out hit, 1 << NavMesh.GetAreaFromName("Walkable")); //NavMesh.GetAreaFromName("Walkable")); // NavMesh.AllAreas);
                        //createAuxin = NavMesh.SamplePosition(new Vector3(x, 0.0f, z), out hit, 0.1f, 1 << NavMesh.GetAreaFromName("Walkable"));
                        //bool isBlocked = _obstacleCollider.bounds.Contains(new Vector3(x, 0.0f, z));
                        Collider[] hitColliders = Physics.OverlapSphere(new Vector3(x, 0f, z), AUXIN_RADIUS + 0.1f, 1 << LayerMask.NameToLayer("Obstacle"));
                        createAuxin = (hitColliders.Length == 0);
                    }

                    //check if auxin can be created there
                    if (createAuxin)
                    {
                        Auxin newAuxin = Instantiate(_auxinPrefab, new Vector3(x, 0.0f, z), Quaternion.identity, auxinPool);

                        //change its name
                        newAuxin.name = "Auxin [" + c + "][" + i + "]";
                        //this auxin is from this cell
                        newAuxin.Cell = _cells[c];
                        //set position
                        newAuxin.Position = new Vector3(x, 0f, z);

                        newAuxin.ShowMesh(SceneController.ShowAuxins);

                        _auxins.Add(newAuxin);
                        //add this auxin to this cell
                        _cells[c].Auxins.Add(newAuxin);

                        //reset the flag
                        flag = 0;

                        //speed up the demonstration a little bit...
                        if (i % 200 == 0)
                            yield return null;
                    }
                    else
                    {
                        //else, try again
                        flag++;
                        i--;
                    }

                    //if flag is above qntAuxins (*2 to have some more), break;
                    if (flag > _maxAuxins * 2)
                    {
                        //reset the flag
                        flag = 0;
                        break;
                    }
                }
            }
        }

        private IEnumerator CreateAgents()
        {
            _agentsContainer = new GameObject("Agents").transform;
          
            //instantiate agents
            foreach (SpawnArea _area in spawnAreas)
            {
                for (int i = 0; i < _area.initialNumberOfAgents; i ++)
                {
                    SpawnNewAgentInArea(_area, true);
                    yield return null;
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            //TODO: Modificar de time-deltatime para fixed frame
            if (!_isReady)
                return;

            foreach (SpawnArea _area in spawnAreas)
            {
                _area.UpdateSpawnCounter(Time.deltaTime);
                if (_area.CycleReady)
                {
                    for (int i = 0; i < _area.quantitySpawnedEachCycle; i++)
                    {
                        SpawnNewAgentInArea(_area, false);
                    }
                }
                _area.ResetCycleReady();
            }

            //reset auxins
            for (int i = 0; i < _cells.Count; i++)
                for (int j = 0; j < _cells[i].Auxins.Count; j++)
                    _cells[i].Auxins[j].ResetAuxin();

            //find nearest auxins for each agent
            for (int i = 0; i < _agents.Count; i++)
                _agents[i].FindNearAuxins();


            for (int i = 0; i < _agents.Count; i++)
                _agents[i].auxinCount = _agents[i].Auxins.Count;
            /*
             * to find where the agent must move, we need to get the vectors from the agent to each auxin he has, and compare with 
             * the vector from agent to goal, generating a angle which must lie between 0 (best case) and 180 (worst case)
             * The calculation formula was taken from the Bicho´s master thesis and from Paravisi OSG implementation.
            */
            /*for each agent:
            1 - for each auxin near him, find the distance vector between it and the agent
            2 - calculate the movement vector 
            3 - calculate speed vector 
            4 - step
            */

            List<Agent> _agentsToRemove = new List<Agent>();
            bool _showAgentAuxingVector = SceneController.ShowAuxinVectors;
            //for (int i = 0; i < _maxAgents; i++)
            for (int i = 0; i < _agents.Count; i++)
            {
                //find the agent
                List<Auxin> agentAuxins = _agents[i].Auxins;

                //vector for each auxin
                for (int j = 0; j < agentAuxins.Count; j++)
                {
                    //add the distance vector between it and the agent
                    _agents[i]._distAuxin.Add(agentAuxins[j].Position - _agents[i].transform.position);

                    //just draw the lines to each auxin
                    if (_showAgentAuxingVector)
                        Debug.DrawLine(agentAuxins[j].Position, _agents[i].transform.position, Color.green);
                }

                //calculate the movement vector
                _agents[i].CalculateDirection();
                //calculate speed vector
                _agents[i].CalculateVelocity();
                //step
                if (!_agents[i].isWaiting)
                    _agents[i].Step();

                _agents[i].WaitStep();
                //if (_agents[i].IsAtCurrentGoal() && !_agents[i].isWaiting)


                if (_agents[i].removeWhenGoalReached && _agents[i].IsAtFinalGoal())
                    _agentsToRemove.Add(_agents[i]);
            }

            foreach(Agent a in _agentsToRemove)
            {
                _agents.Remove(a);
                Destroy(a.gameObject);
            }
            _agentsToRemove.Clear();
        }

        private Cell GetClosestCellToPoint (Vector3 point)
        {
            float _minDist = Vector3.Distance(point, _cells[0].transform.position);
            int _minIndex = 0;
            for (int i = 1; i < _cells.Count; i ++)
            {
                if (Vector3.Distance(point, _cells[i].transform.position) < _minDist)
                {
                    _minDist = Vector3.Distance(point, _cells[i].transform.position);
                    _minIndex = i;
                }
            }

            return _cells[_minIndex];
        }

        private void SpawnNewAgent(Vector3 _pos, bool _removeWhenGoalReached, 
            List<GameObject> _goalList)
        {
            Agent newAgent = Instantiate(_agentPrefabList[Random.Range(0, _agentPrefabList.Count)],
                _pos, Quaternion.identity, _agentsContainer);
            newAgent.name = "Agent [" + GetNewAgentID() + "]";  //name
            newAgent.CurrentCell = GetClosestCellToPoint(_pos);
            newAgent.agentRadius = AGENT_RADIUS;  //agent radius
            newAgent.Goal = _goalList[0];  //agent goal
            newAgent.goalsList = _goalList;
            newAgent.removeWhenGoalReached = _removeWhenGoalReached;
            newAgent.World = this;
            _agents.Add(newAgent);
        }

        private void SpawnNewAgentInArea(SpawnArea _area, bool _isInitialSpawn)
        {
            Vector3 _pos = _area.GetRandomPoint();
            Agent newAgent = Instantiate(_agentPrefabList[Random.Range(0, _agentPrefabList.Count)], 
                _pos, Quaternion.identity, _agentsContainer);
            newAgent.name = "Agent [" + GetNewAgentID() + "]";  //name
            newAgent.CurrentCell = GetClosestCellToPoint(_pos);
            newAgent.agentRadius = AGENT_RADIUS;  //agent radius
            if (_isInitialSpawn)
            {
                newAgent.Goal = _area.initialAgentsGoalList[0];  //agent goal
                newAgent.goalsList = _area.initialAgentsGoalList;
                newAgent.removeWhenGoalReached = _area.initialRemoveWhenGoalReached;
                newAgent.goalsWaitList = _area.initialWaitList;
            }
            else
            {
                newAgent.Goal = _area.repeatingGoalList[0];  //agent goal
                newAgent.goalsList = _area.repeatingGoalList;
                newAgent.removeWhenGoalReached = _area.repeatingRemoveWhenGoalReached;
                newAgent.goalsWaitList = _area.repeatingWaitList;
            }
            newAgent.World = this;
            _agents.Add(newAgent);
        }

        private int GetNewAgentID()
        {
            _newAgentID++;
            return _newAgentID - 1;
        }

        public void ShowAuxinMeshes (bool p_enable)
        {
            foreach (Auxin _a in Auxins)
                _a.ShowMesh(p_enable);
        }
        public void ShowCellMeshes(bool p_enable)
        {
            foreach (Cell _c in Cells)
                _c.ShowMesh(p_enable);
        }
    }
}