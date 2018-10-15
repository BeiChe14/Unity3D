// Shatter Toolkit
// Copyright 2018 PHY
using System.Collections;
using UnityEngine;

public class shatter_split : MonoBehaviour
{
	public ShatterScheduler scheduler = null;
	public int raycastCount = 10;
	private Vector3 start, end;
	private Ray ray;
	private RaycastHit hit;
	private bool slice_start = false;
	private bool slice_end = false;
	private float Screen_min = 0f;
	private float Screen_max = 1f;
	private float Screen_step = 0.1f;

	public void Start ()
	{
		enabled = true;
		scheduler = Camera.main.GetComponent<ShatterScheduler> ();

		Screen_step = 1.0f / raycastCount;
		//Debug.Log (Screen_step);

		slice_point [0] = new Vector3 (0f, 0f, 0f);
		slice_point [1] = new Vector3 (1f*Screen.width, 0f, 0f);
		slice_point [2] = new Vector3 (1f*Screen.width, 1f*Screen.height, 0f);
		slice_point [3] = new Vector3 (0f, 1f*Screen.height, 0f);
		
		slice_model [0] = new ArrayList ();//slice_point 0-1 
		slice_model [1] = new ArrayList ();//slice_point 1-2
		slice_model [2] = new ArrayList ();//slice_point 2-3
		slice_model [3] = new ArrayList ();//slice_point 3-0
	}

	public void Update ()
	{		
		if (!slice_start) {
			slice_start = true;

			model_select ();
			model_clone ();
			StartCoroutine(model_slice ());			
		}
	}

	Vector3[] slice_point = new Vector3[4];
	ArrayList[] slice_model = new ArrayList[4];
	ArrayList splitPlane = new ArrayList();
	ArrayList clone_model = new ArrayList ();
	ArrayList dectect_model = new ArrayList ();//only slice

	private Vector3 ulc = new Vector3();
	private Vector3 lrc = new Vector3();

    int split_plane_len = 0;

	void Execute (Vector3 s, Vector3 e,int index)
	{
        //liner.enabled = true;
        start = s;
		end = e;
			
		// Calculate the world-space line
		Camera mainCamera = Camera.main;
			
		float near = mainCamera.near;
			
		Vector3 line = mainCamera.ScreenToWorldPoint (new Vector3 (end.x, end.y, near)) - mainCamera.ScreenToWorldPoint (new Vector3 (start.x, start.y, near));
			
		// Find game objects to split by raycasting at points along the line
		for (int i = 0; i < raycastCount; i++) {
			Ray ray = mainCamera.ScreenPointToRay (Vector3.Lerp (start, end, (float)i / raycastCount));
			RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (i == 1 && start.Equals(slice_point[1]))
                {
                    ulc = GameObject.Find(hit.collider.name).transform.TransformPoint(hit.point);
                }
                if (i == (raycastCount - 1) && start.Equals(slice_point[2]))
                {
                    lrc = GameObject.Find(hit.collider.name).transform.TransformPoint(hit.point);
                }
                if (split_plane_len > splitPlane.Count)
                {
                    splitPlane.Add(new Plane(Vector3.Normalize(Vector3.Cross(line, ray.direction)), hit.point));
                }
                if (split_plane_len == splitPlane.Count)
                {
                    splitPlane.Add(new Plane(Vector3.Normalize(Vector3.Cross(line, ray.direction)), hit.point));
                }
                if (!dectect_model.Contains (hit.collider.name)) {
					dectect_model.Add (hit.collider.name);
				}
				if ((!slice_model [index].Contains (hit.collider.name))) {
					slice_model [index].Add (hit.collider.name);
				}
			}
		}
        split_plane_len++;
    }

	private LineRenderer line;
	
	void model_select ()
	{
		//1.slice_model
		for (int i=0; i<4; i++) {
			if (i == 3) {
				Execute (slice_point [3], slice_point [0],3);
			} else {
				Execute (slice_point [i], slice_point [i + 1],i);
			}
		}

        //2.clone_model
		for (float x=Screen_min+Screen_step; x<Screen_max; x+=Screen_step) {
			for (float y=Screen_min; y<Screen_max; y+=Screen_step) {
				ray = Camera.main.ViewportPointToRay (new Vector3 (x, y, 0)); 
				if (Physics.Raycast (ray, out hit)) {
					if (!dectect_model.Contains (hit.collider.name)) {
						if (!clone_model.Contains (hit.collider.name)) {
                            clone_model.Add(hit.collider.name);
						}
					}
				}
			}
		}
		Debug.Log ("待克隆模型数:" + clone_model.Count);
	}

    bool IsInCamera(string name) {
        Transform t = GameObject.Find(name).transform;
        Vector3 position = t.position;
        Vector3 camera = Camera.main.WorldToViewportPoint(position);
        Debug.Log(name+"\t"+camera);
        if (camera.x >= 0f && camera.x <= 1f && camera.y >= 0f && camera.y <= 1f)
            return true;
        else
            return false;
    }
	
	ArrayList _3ds = new ArrayList ();
	GameObject bc;
	GameObject ac;
	
	void model_clone ()
	{
		for (int i=0; i<clone_model.Count; i++) {
			bc = GameObject.Find (clone_model [i].ToString ());
			ac = Instantiate (bc) as GameObject;
			
			//bc.SetActive (false);

			ac.transform.position = bc.transform.position;
			ac.transform.rotation = bc.transform.rotation;
			ac.transform.localScale = bc.transform.localScale;
			ac.name = bc.name + "_C";
			//ac.SetActive(true);

			_3ds.Add (bc.name);
		}
	}
	
	IEnumerator model_slice ()
	{   
		string name;
		bool finish = false;
        for (int i = 0; i < 4; i++) {
            for (int j = 0; j < slice_model[i].Count; j++)
            { 
                name = slice_model[i][j].ToString();
           
                if (!_3ds.Contains(name))
                    _3ds.Add(name);
                if (i > 0)
                {
                    if (GameObject.Find(name + "_C") != null)
                    {
                        name = name + "_C";
                    }
                }
				if (scheduler != null) {
					scheduler.AddTask (new SplitTask (GameObject.Find(name), new Plane[] { (Plane)splitPlane[i] }));
				} 
				if(i==3)
					finish = true;
			}
			yield return new WaitForSeconds(0.1f);
		}
        if (finish)
			Invoke ("model_show",0.1f);
	}

	public GameObject Empty;
	private GameObject temp;

	void model_show ()
	{
        
		for (int i=0; i<_3ds.Count; i++) {
			temp = GameObject.Find(_3ds[i].ToString()+"_C");
			temp.transform.SetParent(Empty.transform,true);
		}
        
        //GameObject.Find("GameObject").transform.Translate(0,30f,0);
        
        model_export();
	}

	void model_export(){
		Debug.Log ("The upper left corner is " + ulc.ToString());
		Debug.Log("The lower right corner is " + lrc.ToString());
        ObjExporter.DoExport(ulc,lrc);
    }
}
