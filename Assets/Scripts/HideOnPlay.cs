using System.Collections;
using UnityEngine;

// This class is used to hide editor preview objects when entering playmode.
public class HideOnPlay : MonoBehaviour {

	void Start () {
		gameObject.SetActive (false);
	}

}