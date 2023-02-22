using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveObject : MonoBehaviour
{
    //[HideInInspector]
    public GameObject targetGameObject;
    public LevelEditorUIController editorUIController;


    //[HideInInspector]
    public bool isSelected = false;

    private ManagerScript ms;
    private Vector3 originalVector3;
    private Vector3 mousePos;


    public GameObject selectionCircle;

    // Start is called before the first frame update
    void Start()
    {
        ms = GetComponent<MouseScript>().ms;
    }

    // Update is called once per frame
    void Update()
    {
        selectionCircle.transform.Rotate(Vector3.forward * 90f * Time.deltaTime);

        

        if (isSelected)
        {
            Vector3 _terrainSize = editorUIController.mainSimulationScenario.terrain.terrainData.size;
            Vector3 _parentPosition = targetGameObject.transform.parent.position;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelMoveObject();
            } 
            else
            {
                
                mousePos = Input.mousePosition;
                mousePos = Camera.main.ScreenToWorldPoint(mousePos);

                //targetGameObject.transform.position = new Vector3(
                //    transform.position.x,
                //    0.5f,
                //    transform.position.z);

                targetGameObject.transform.position = new Vector3(
                    Mathf.Clamp(transform.position.x, _parentPosition.x, _parentPosition.x + _terrainSize.x),
                    0.5f,
                    Mathf.Clamp(transform.position.z, _parentPosition.z, _parentPosition.z + _terrainSize.z)
                    );
            }
            selectionCircle.transform.position = new Vector3(
                    Mathf.Clamp(transform.position.x, _parentPosition.x, _parentPosition.x + _terrainSize.x),
                    0.1f,
                    Mathf.Clamp(transform.position.z, _parentPosition.z, _parentPosition.z + _terrainSize.z)
                    );
            var maxScale = Mathf.Max(targetGameObject.transform.localScale.x, targetGameObject.transform.localScale.z);
            selectionCircle.transform.localScale = Vector3.one * (maxScale * 1.3f);
        }
        else
        {
            selectionCircle.transform.position = Vector3.one * -1000f;
        }
    }

    public void SelectObject(GameObject go)
    {
        targetGameObject = go;
        originalVector3 = go.transform.position;
        isSelected = true;
        if (go.tag == "Goal")
            go.GetComponent<MeshRenderer>().material = ms.world.prefabManager.GetGoalPrefab().GetComponent<MeshRenderer>().sharedMaterial;
    }

    public void CancelMoveObject()
    {
        isSelected = false;
        targetGameObject.transform.position = originalVector3;
    }
}
