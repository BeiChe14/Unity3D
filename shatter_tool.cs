// Shatter Toolkit
// Copyright 2012 Gustav Olsson
using System.Collections.Generic;
using UnityEngine;

public class shatter_tool : MonoBehaviour
{
	[SerializeField]
	private int generation = 1；
	
	[SerializeField]
	private int generationLimit = 3；
	
	[SerializeField]
	private int cuts = 2；
	
	[SerializeField]
	private bool fillCut = true;
	
	[SerializeField]
	private HullType internalHullType = HullType.FastHull;
	
	private IHull hull;
	
	private Vector3 center;

	private UvMapper uvMapper;

	/// <summary>
	/// Gets or sets the current generation of this ShatterTool instance.
	/// By default, a game object is of generation 1. When a game object
	/// is shattered using ShatterTool.Shatter() all new game objects
	/// will be considered of generation 2, and so on.
	/// For example, this value can be used to vary the color of a
	/// game object depending on how many times it has been shattered.
	/// </summary>
	public int Generation
	{
		get { return generation; }
		set { generation = Mathf.Max(value, 1); }
	}
	
	/// <summary>
	/// Gets or sets the generation limit of this ShatterTool instance.
	/// This value restricts how many times a game object may be shattered
	/// using ShatterTool.Shatter(). A game object will only be able to shatter
	/// if ShatterTool.Generation is less than ShatterTool.GenerationLimit.
	/// </summary>
	public int GenerationLimit
	{
		get { return generationLimit; }
		set { generationLimit = Mathf.Max(value, 1); }
	}
	
	/// <summary>
	/// Gets or sets the number of times the game object will be cut when ShatterTool.Shatter() occurs.
	/// </summary>
	public int Cuts
	{
		get { return cuts; }
		set { cuts = Mathf.Max(value, 1); }
	}
	
	/// <summary>
	/// Gets or sets whether the cut region should be triangulated.
	/// If true, the connected UvMapper component will control the vertex properties of the filled area.
	/// When the ShatterTool is used on double-sided meshes with zero thickness, such as planes, this value
	/// should be false.
	/// </summary>
	public bool FillCut
	{
		get { return fillCut; }
		set { fillCut = value; }
	}
	
	/// <summary>
	/// Gets or sets the type of the internal hull used to shatter the mesh. The FastHull implementation is roughly 20-50% faster
	/// than the LegacyHull implementation and requires no time to startup. The LegacyHull implementation is more robust in extreme
	/// cases and is provided for backwards compatibility. This setting can't be changed during runtime.
	/// </summary>
	public HullType InternalHullType
	{
		get { return internalHullType; }
		set { internalHullType = value; }
	}
	
	/// <summary>
	/// Determines whether this game object is of the first generation. (Generation == 1)
	/// </summary>
	public bool IsFirstGeneration
	{
		get { return generation == 1; }
	}
	
	/// <summary>
	/// Determines whether this game object is of the last generation. (Generation >= GenerationLimit)
	/// </summary>
	public bool IsLastGeneration
	{
		get { return generation >= generationLimit; }
	}
	
	/// <summary>
	/// Gets the worldspace center of the game object. Only works during runtime.
	/// </summary>
	public Vector3 Center
	{
		get { return transform.TransformPoint(center); }
	}
	
	private void CalculateCenter()
	{
		// Get the localspace center of the mesh bounds
		center = mf.sharedMesh.bounds.center;
	}
	
	private MeshFilter mf;
	private Mesh sharedMesh;

	public void Start()
	{
		mf = gameObject.GetComponent<MeshFilter> ();
		uvMapper = Camera.main.GetComponent<UvMapper> ();
		sharedMesh = mf.sharedMesh;

		// Initialize the first generation hull
		if (hull == null)
		{
			if (internalHullType == HullType.FastHull)
			{
				hull = new FastHull(sharedMesh);
			}
			else if (internalHullType == HullType.LegacyHull)
			{
				hull = new LegacyHull(sharedMesh);
			}
		}
		
		// Update properties
		CalculateCenter();
	}
	
	/// <summary>
	/// Splits the game object using an array of planes, instantiating the pieces as new
	/// game objects (clones of the original) and destroying the original game object when finished.
	/// </summary>
	/// <param name="planes">
	/// An array of world-space planes with unit-length normals.
	/// </param>
	public void Split(Plane[] planes)
	{
		if (planes != null && planes.Length > 0 && hull != null && !hull.IsEmpty)
		{	

			if (uvMapper != null)
			{	
				Vector3[] points, normals;
				
				ConvertPlanesToLocalspace(planes, out points, out normals);
				
				IList<IHull> newHulls;
				
				CreateNewHulls(uvMapper, points, normals, out newHulls);
				
				GameObject[] newGameObjects;
				
				CreateNewGameObjects(newHulls, out newGameObjects);

			}
			else
			{
			
			}
		}
	}
	
	private void ConvertPlanesToLocalspace(Plane[] planes, out Vector3[] points, out Vector3[] normals)
	{
		points = new Vector3[planes.Length];
		normals = new Vector3[planes.Length];
		
		for (int i = 0; i < planes.Length; i++)
		{
			Plane plane = planes[i];
			
			Vector3 localPoint = transform.InverseTransformPoint(plane.normal * -plane.distance);
			Vector3 localNormal = transform.InverseTransformDirection(plane.normal);
			
			localNormal.Scale(transform.localScale);
			localNormal.Normalize();
			
			points[i] = localPoint;
			normals[i] = localNormal;
		}
	}
	
	private void CreateNewHulls(UvMapper uvMapper, Vector3[] points, Vector3[] normals, out IList<IHull> newHulls)
	{
		newHulls = new List<IHull>();
		
		// Add the starting hull
		newHulls.Add(hull);
		
		for (int j = 0; j < points.Length; j++)
		{
			int previousHullCount = newHulls.Count;
			
			for (int i = 0; i < previousHullCount; i++)
			{
				IHull previousHull = newHulls[0];
				
				// Split the previous hull
				IHull a, b;
				
				previousHull.Split(points[j], normals[j], fillCut, uvMapper, out a, out b);
				
				// Update the list
				newHulls.Remove(previousHull);
				
				if (!a.IsEmpty)
				{
					newHulls.Add(a);
				}
				
				if (!b.IsEmpty)
				{
					newHulls.Add(b);
				}
			}
		}
	}
	
	private void CreateNewGameObjects(IList<IHull> newHulls, out GameObject[] newGameObjects)
	{
		// Get new meshes
		Mesh[] newMeshes = new Mesh[newHulls.Count];
		float[] newVolumes = new float[newHulls.Count];
		
		float totalVolume = 0.0f;
		
		for (int i = 0; i < newHulls.Count; i++)
		{
			Mesh mesh = newHulls[i].GetMesh();
			Vector3 size = mesh.bounds.size;
			float volume = size.x * size.y * size.z;
			
			newMeshes[i] = mesh;
			newVolumes[i] = volume;
			
			totalVolume += volume;
		}

		// Create new game objects
		newGameObjects = new GameObject[newHulls.Count];
		
		for (int i = 0; i < newHulls.Count; i++)
		{
			IHull newHull = newHulls[i];
			Mesh newMesh = newMeshes[i];
			float volume = newVolumes[i];
			
			GameObject newGameObject = (GameObject)Instantiate (gameObject);
			
			// Set mesh filter
			MeshFilter newMeshFilter = newGameObject.GetComponent<MeshFilter>();
			
			if (newMeshFilter != null)
			{
				newMeshFilter.sharedMesh = newMesh;
			}

			// Set mesh collider
			MeshCollider newMeshCollider = newGameObject.GetComponent<MeshCollider>();
			
			if (newMeshCollider != null)
			{
				newMeshCollider.sharedMesh = newMesh;
			}

			newGameObject.transform.position = gameObject.transform.position;
			newGameObject.transform.rotation = gameObject.transform.rotation;

			if(i==0)
				GameObject.Destroy(newGameObject);
			else{
				if(!gameObject.name.Substring(gameObject.name.Length-2).Equals("_C"))
					newGameObject.name = gameObject.name + "_C";
				else{
					newGameObject.name = gameObject.name;
					GameObject.Destroy(gameObject);
				}
			}

			newGameObjects[i] = newGameObject;
		}
	}
}