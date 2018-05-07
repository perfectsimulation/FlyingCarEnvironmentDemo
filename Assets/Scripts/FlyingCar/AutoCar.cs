using UnityEngine;
using System.Collections;

// This class represents the motor functions of a dummy autonomous vehicle
public class AutoCar : MonoBehaviour {

	public float forwardSpeed = 90f;
	public float turnSpeed = 0.6f;
	public float verticalSpeed = 30f;

	public float hoverForce = 65f;
	public float hoverHeight = 30f;

	public Route [] route;

	float powerInput;
	float turnInput;

	Rigidbody carRigidbody;

	void Awake () {
		carRigidbody = GetComponent<Rigidbody> ();
	}

	void Start () {
		StartCoroutine (ProceedOnRoute ());
	}

	void FixedUpdate () {
		Ray ray = new Ray (transform.position, -transform.up);
		RaycastHit hit;

		if (Physics.Raycast (ray, out hit, hoverHeight)) {
			float proportionalHeight = (hoverHeight - hit.distance) / hoverHeight;
			Vector3 appliedHoverForce = Vector3.up * proportionalHeight * hoverForce;
			carRigidbody.AddForce (appliedHoverForce, ForceMode.Acceleration);
		}
			
	}

	IEnumerator ProceedOnRoute () {
		foreach (Route instruction in route) {
			float timeElapsed = 0f;
			while (timeElapsed < instruction.time) {
				
				carRigidbody.AddRelativeForce (0f, 0f, instruction.forwardMagnitude); // move forward or backward
				carRigidbody.AddRelativeTorque (0f, instruction.turnMagnitude, 0f); // turn left or right
				carRigidbody.AddRelativeForce (0f, instruction.verticalMagnitude, 0f); // move upward or downward

				timeElapsed += Time.deltaTime;
				yield return new WaitForSeconds (Time.deltaTime);
			}
		}
	}
}

[System.Serializable]
public struct Route {
	public float time;
	public float forwardMagnitude;
	public float turnMagnitude;
	public float verticalMagnitude;
}