using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // variables
    // [SerializeField] private float moveSpeed;
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    [SerializeField] private bool running;
    [SerializeField] private float turnRatio;

    private Vector3 moveDirection;

    // referemces
    private CharacterController controller;
    private Animator animator;

    // Start is called before the first frame update
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    private void Update()
    {
        Move();
    }

    private void Move() 
    {
        float moveVertical = Input.GetAxis("Vertical");
        float moveHorizontal = Input.GetAxis("Horizontal");

        moveDirection = new Vector3(moveHorizontal,0,moveVertical);

        if (moveDirection != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, turnRatio * Time.deltaTime);
        }

        if(Input.GetKey(KeyCode.LeftShift) && moveDirection != Vector3.zero)
        {
            moveDirection *= runSpeed;
            animator.SetFloat("Speed", 0.5f);
        }
        else if(!Input.GetKey(KeyCode.LeftShift) && moveDirection != Vector3.zero)
        {
            moveDirection *= walkSpeed;
            animator.SetFloat("Speed", 0.25f);
        }
        else
        {
            animator.SetFloat("Speed", 0);
        }

        controller.Move(moveDirection * Time.deltaTime);
    }
}
