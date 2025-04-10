using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SFB_DemoGelatinousControl : MonoBehaviour {

	public Material[] cubeMaterials;
	public Material[] sphereMaterials;
	public GameObject cube;
	public GameObject sphere;
	public Animator cubeAnimator;
	public Animator sphereAnimator;

	public void Locomotion(float newValue){
		cubeAnimator.SetFloat ("locomotion", newValue);
		sphereAnimator.SetFloat ("locomotion", newValue);
	}

	public void SetWalk(float newValue){
		cubeAnimator.SetFloat ("walk", newValue);
		sphereAnimator.SetFloat ("walk", newValue);
	}

	public void SetMaterial(int newValue){
		Material[] cubeMaterial;
		Material[] sphereMaterial;
		cubeMaterial = cube.GetComponent<Renderer> ().materials;
		sphereMaterial = sphere.GetComponent<Renderer> ().materials;
		cubeMaterial [0] = cubeMaterials [newValue];
		sphereMaterial [0] = sphereMaterials [newValue];
		cube.GetComponent<Renderer> ().materials = cubeMaterial;
		sphere.GetComponent<Renderer> ().materials = sphereMaterial;
	}

	public void RandomMaterial()
	{
		Material[] cubeMaterial;
		Material[] sphereMaterial;
		cubeMaterial = cube.GetComponent<Renderer> ().materials;
		sphereMaterial = sphere.GetComponent<Renderer> ().materials;
		cubeMaterial [0] = cubeMaterials [Random.Range(0, cubeMaterials.Length)];
		sphereMaterial [0] = sphereMaterials [Random.Range(0, sphereMaterials.Length)];
		cube.GetComponent<Renderer> ().materials = cubeMaterial;
		sphere.GetComponent<Renderer> ().materials = sphereMaterial;
	}

	public Button button;

	public void SuperRandom() => button.onClick.Invoke();

	public void Update()
	{
		if (Input.GetKeyDown(KeyCode.R))
			SuperRandom();
	}
}
