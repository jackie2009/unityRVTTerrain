using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
 

public class Test : MonoBehaviour {

	public float startZ;
	public float endZ;
	public float speed = 1;
	public float hight = 1;
	public  GameObject[] switchItem;
	public Terrain terrain;
	private float startTime = 0;
	private bool moving = true;
	[ContextMenu("clear")]
	void clear() {
		var td=GetComponent<Terrain>().terrainData;
	 
		td.splatPrototypes= new SplatPrototype[1] { td.splatPrototypes[0] };
		td.RefreshPrototypes();
		GetComponent<Terrain>().terrainData = td;
	}
	
	// Update is called once per frame
	void Update () {
		float lerp = 0.1f;
		if (Input.GetKeyDown(KeyCode.R))
		{
			lerp = 1;
			startTime = Time.time;
		 
			//UnityEditor.EditorApplication.isPaused = true;
		}
		if (Input.GetKeyDown(KeyCode.P)) {
			
			switchItem[0].SetActive(!switchItem[0].activeSelf);
			switchItem[1].SetActive(!switchItem[1].activeSelf);
		}
		if (Input.GetKeyDown(KeyCode.Q))
		{
			 
				moving = !moving;
			 
		
		}
		if (moving == false) return;
		var pos = transform.position;
		pos.z = startZ + Mathf.PingPong(endZ - startZ + (Time.time - startTime) * speed, endZ - startZ);
		pos.y = terrain.SampleHeight(pos) + hight;
		transform.position = Vector3.Lerp(transform.position, pos, lerp);
	}

	List<Vector3> circleList = new List<Vector3>();
	float minCircle = 0.3f;
	float maxCircle = 1.0f;
	float getSdf(Vector2 pos,Rect rect) {
		Vector2 limit = new Vector2(Mathf.Min( pos.x, rect.width - pos.x), Mathf.Min(pos.y, rect.height - pos.y));
		float sdf = Mathf.Min(limit.x, limit.y, maxCircle);
        foreach (var c in circleList)
        {
			float d = Vector2.Distance(pos, c) - c.z;
			
			if (d < minCircle) return -1;
			sdf = Mathf.Min(d, sdf);

        }
		if (sdf < minCircle) return -1;
		return  sdf;
	}
  
	void Start() {
 

	}
 
}
