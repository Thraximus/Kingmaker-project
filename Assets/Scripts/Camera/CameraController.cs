using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform mainCamera;

    [SerializeField] private float normalCameraSpeed;
    [SerializeField] private float maxCameraZoom;
    [SerializeField] private float minCameraZoom;
    [SerializeField] private float fastCameraSpeed;
    [SerializeField] private float cameraMovementTime;
    [SerializeField] private float cameraRotationAmount;
    [SerializeField] private Vector3 cameraZoomAmount;

    private Vector3 newPosition;
    private Quaternion newRotation;
    private Vector3 newZoom;
    private Vector3 rotateStartPos;
    private Vector3 rotateCurrentPos;
    private bool edgeMoving = false;

    private float edgeSize = 10f;

    
    // Start is called before the first frame update
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
        newPosition = transform.position;
        newRotation = transform.rotation;
        newZoom = mainCamera.localPosition;
    }

    // Update is called once per frame
    private void Update()
    {
        HandleMouseInput();
        HandleKeyboardInput();
        commitCameraChanges();
    }

    private float GetCameraSpeed()
    {
        if(Input.GetKey(KeyCode.LeftShift))
        {
            return fastCameraSpeed;
        }
        else
        {
            return normalCameraSpeed;
        }
    }

    private void HandleKeyboardInput()
    {
        HandleKeyboardMovement();
        HandleKeyboardRotation();
        HandleKeyboardZoom();
    }

    private void HandleMouseInput()
    {
        HandleMouseMovement();
        HandleMouseRotation();
        HandleMouseZoom();
    }

    private void HandleMouseMovement()
    {
        edgeMoving = false;
        // move camera right
        if(Input.mousePosition.x > Screen.width - edgeSize)
        {
            newPosition += (transform.right * GetCameraSpeed());
            edgeMoving = true;
        }
        // move camera left
        if(Input.mousePosition.x < 0 + edgeSize)
        {
            newPosition += (transform.right * -GetCameraSpeed());
            edgeMoving = true;
        }

        // move camera down
        if(Input.mousePosition.y < 0 + edgeSize)
        {
            newPosition += (transform.forward * -GetCameraSpeed());
            edgeMoving = true;
        }
        // move camera up
        if(Input.mousePosition.y > Screen.height - edgeSize)
        {
            newPosition += (transform.forward * GetCameraSpeed());
            edgeMoving = true;
        }
    }

    private void HandleMouseRotation()
    {
        if(Input.GetMouseButtonDown(2))
        {
            rotateStartPos = Input.mousePosition;
        }
        if(Input.GetMouseButton(2))
        {
            rotateCurrentPos = Input.mousePosition;

            Vector3 difference = rotateStartPos - rotateCurrentPos;

            rotateStartPos = rotateCurrentPos;

            newRotation *= Quaternion.Euler(Vector3.up * (-difference.x / 5f));
        }

    }

    private void HandleMouseZoom()
    {
        if(Input.mouseScrollDelta.y != 0)
        {
            newZoom += Input.mouseScrollDelta.y * (cameraZoomAmount*7) ;
        }
    }


    private void HandleKeyboardMovement()
    {
        if (edgeMoving == false)
        {
            if(Input.GetKey(KeyCode.UpArrow))
            {
                newPosition += (transform.forward * GetCameraSpeed());
            }
            if(Input.GetKey(KeyCode.DownArrow))
            {
                newPosition += (transform.forward * -GetCameraSpeed());
            }
            if(Input.GetKey(KeyCode.LeftArrow))
            {
                newPosition += (transform.right * -GetCameraSpeed());
            }
            if(Input.GetKey(KeyCode.RightArrow))
            {
                newPosition += (transform.right * GetCameraSpeed());
            }
        }

        
    }

    private void HandleKeyboardRotation()
    {
        if(Input.GetKey(KeyCode.LeftBracket))
        {
            newRotation *= Quaternion.Euler(Vector3.up * cameraRotationAmount);
        }
        
        if(Input.GetKey(KeyCode.RightBracket))
        {
            newRotation *= Quaternion.Euler(Vector3.up * -cameraRotationAmount);
        }

        
    }

    private void HandleKeyboardZoom()
    {
        if(Input.GetKey(KeyCode.KeypadPlus))
        {
            newZoom += cameraZoomAmount;
        }
        if(Input.GetKey(KeyCode.KeypadMinus))
        {
            newZoom -= cameraZoomAmount;
        }
    }

    private void commitCameraChanges()
    {
        transform.position = Vector3.Lerp(transform.position, newPosition,Time.deltaTime * cameraMovementTime); // camera movement
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * cameraMovementTime); // camera rotation
        if(newZoom.y < minCameraZoom)
        {
            newZoom.y = minCameraZoom;
        }
        else if(newZoom.y > maxCameraZoom) 
        {
            newZoom.y = maxCameraZoom;
        }  

        if(newZoom.z > minCameraZoom-5)
        {
            newZoom.z = minCameraZoom-5;
        }
        else if(newZoom.z < -maxCameraZoom+5)
        {
            newZoom.z = -maxCameraZoom+5;
        }
        mainCamera.localPosition = Vector3.Lerp(mainCamera.localPosition, newZoom, Time.deltaTime * cameraMovementTime).Round(4); // camera zoom
    }
}
