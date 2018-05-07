using UnityEngine;
using System.Collections;

// This class represents the motor functions of a hovering car
public class HoverMotor : MonoBehaviour {

	public float forwardSpeed = 90f;
	public float turnSpeed = 0.6f;
	public float verticalSpeed = 30f;

	public float hoverForce = 65f;
	public float hoverHeight = 30f;

	float powerInput;
	float turnInput;

	bool moveUp = false;
	bool moveDown = false;

	Rigidbody carRigidbody;

	void Awake () {
		carRigidbody = GetComponent<Rigidbody> ();
	}

	void Update () {
		powerInput = Input.GetAxis ("Vertical"); // forward and backward
		turnInput = Input.GetAxis ("Horizontal"); // left and right turns

		if (Input.GetButton ("Up")) {
			moveUp = true;
			moveDown = false;
		} else if (Input.GetButton ("Down")) {
			moveUp = false;
			moveDown = true;
		} else {
			moveUp = false;
			moveDown = false;
		}
	}

	void FixedUpdate () {
		Ray ray = new Ray (transform.position, -transform.up);
		RaycastHit hit;

		if (Physics.Raycast (ray, out hit, hoverHeight)) {
			float proportionalHeight = (hoverHeight - hit.distance) / hoverHeight;
			Vector3 appliedHoverForce = Vector3.up * proportionalHeight * hoverForce;
			carRigidbody.AddForce (appliedHoverForce, ForceMode.Acceleration);
		}

		carRigidbody.AddRelativeForce (0f, 0f, powerInput * forwardSpeed); // move forward or backward
		carRigidbody.AddRelativeTorque (0f, turnInput * turnSpeed, 0f); // turn left or right

		if (moveUp) {
			carRigidbody.AddRelativeForce (0f, verticalSpeed, 0f); // move upward
		} else if (moveDown) {
			carRigidbody.AddRelativeForce (0f, -verticalSpeed, 0f); // move downward

		}
	}
}