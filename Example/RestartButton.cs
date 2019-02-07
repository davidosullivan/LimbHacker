using UnityEngine;
using System.Collections;

[RequireComponent(typeof(RayHitButton))]
public class RestartButton : MonoBehaviour
{
	public Spawner spawner;

	private RayHitButton button;

	void Start() {
		button = gameObject.GetComponent<RayHitButton>();
	}

	void Update () {
		button.visible = spawner.CanInstantiate;
	}

	void OnClick() {
		spawner.Instantiate();
	}
}
