using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This class provides a procedural generator for terrain.
public class EndlessTerrain : MonoBehaviour {

	const float scale = 1f; // terrain scale factor

	const float viewerMoveThresholdForTerrainUpdate = 25f; // only update visible terrain if viewer has moved by this amount
	const float sqrViewerMoveThresholdForTerrainUpdate = viewerMoveThresholdForTerrainUpdate * viewerMoveThresholdForTerrainUpdate;

	public LODInfo [] detailLevels; // how detailed the mesh is according to how far away the viewer is
	public static float maxViewDistance; // the farthest terrain to render

	public Transform viewer; // player point of view
	public Material mapMaterial; // material to render terrain on

	public static Vector2 viewerPosition;
	Vector2 viewerPositionOld;

	static MapGenerator mapGenerator;
	int chunkSize; // actual size of terrain chunk
	int chunksVisibleInViewDistance;

	Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk> (); // keep track of generated terrain so new chunks are only generated at edge of traveled regions
	static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk> (); // list of active terrain chunks

	// start by giving chunkSize, viewDistance, and chunksVisible values
	void Start () {
		mapGenerator = FindObjectOfType<MapGenerator> ();

		maxViewDistance = detailLevels [detailLevels.Length - 1].visibleDistanceThreshold; // farthest terrain to render
		chunkSize = MapGenerator.mapChunkSize - 1; // want actual terrain chunk size one unit smaller to fit all together seamlessly
		chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);

		UpdateVisibleChunks ();
	}

	// keeps track of where the viewer is and updates terrain when necessary
	void Update () {
		viewerPosition = new Vector2 (viewer.position.x, viewer.position.z) / scale; // keep track of where the viewer is at all times

		if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForTerrainUpdate) { // only update terrain if viewer has moved more than the threshold move distance
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunks ();
		}
	}

	// sends updates to all the terrain chunks that should be visible when the viewer is within the view distance
	void UpdateVisibleChunks () {

		for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++) {
			terrainChunksVisibleLastUpdate [i].SetVisible (false); // set all past terrain chunks to not visible
		}
		terrainChunksVisibleLastUpdate.Clear (); // clear the list so no terrain chunks are erroneously rendered from the last update

		int currentChunkCoordX = Mathf.RoundToInt (viewerPosition.x / chunkSize); // terrain chunk the viewer is located on
		int currentChunkCoordY = Mathf.RoundToInt (viewerPosition.y / chunkSize);

		for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++) { // loop thru the chunks within view distance
			for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++) {
				Vector2 viewedChunkCoord = new Vector2 (currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

				if (terrainChunkDictionary.ContainsKey (viewedChunkCoord)) { // if the terrain chunk has already been generated previously
					terrainChunkDictionary [viewedChunkCoord].UpdateTerrainChunk ();
				} else { // else create a new terrain chunk
					terrainChunkDictionary.Add (viewedChunkCoord, new TerrainChunk (viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
				}

			}
		}
	}

	// This class represents a terrain chunk object
	public class TerrainChunk {

		GameObject meshObject;
		Vector2 position;
		Bounds bounds;

		MeshRenderer meshRenderer;
		MeshFilter meshFilter;
		MeshCollider meshCollider;

		LODInfo [] detailLevels;
		LODMesh [] lodMeshes;
		LODMesh collisionLODMesh;

		MapData mapData;
		bool mapDataReceived;
		int previousLODIndex = -1;

		public TerrainChunk (Vector2 coord, int size, LODInfo [] detailLevels, Transform parent, Material material) {
			this.detailLevels = detailLevels;

			position = coord * size;
			bounds = new Bounds (position,Vector2.one * size);
			Vector3 positionV3 = new Vector3 (position.x,0,position.y);

			meshObject = new GameObject ("Terrain Chunk");
			meshRenderer = meshObject.AddComponent<MeshRenderer> ();
			meshFilter = meshObject.AddComponent<MeshFilter> ();
			meshCollider = meshObject.AddComponent<MeshCollider> ();
			meshRenderer.material = material;

			meshObject.transform.position = positionV3 * scale;
			meshObject.transform.parent = parent;
			meshObject.transform.localScale = Vector3.one * scale;
			SetVisible (false);

			lodMeshes = new LODMesh [detailLevels.Length];
			for (int i = 0; i < detailLevels.Length; i++) {
				lodMeshes [i] = new LODMesh (detailLevels [i].lod, UpdateTerrainChunk);
				if (detailLevels [i].useForCollider) {
					collisionLODMesh = lodMeshes [i];
				}
			}

			mapGenerator.RequestMapData (position, OnMapDataReceived);
		}

		// waits for map data to finish generating on a separate thread
		void OnMapDataReceived (MapData mapData) {
			this.mapData = mapData;
			mapDataReceived = true;

			Texture2D texture = TextureGenerator.TextureFromColorMap (mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
			meshRenderer.material.mainTexture = texture;

			UpdateTerrainChunk ();
		}

		// updates a single terrain chunk
		public void UpdateTerrainChunk () {
			if (mapDataReceived) {
				float viewerDistanceFromNearestEdge = Mathf.Sqrt (bounds.SqrDistance (viewerPosition));
				bool visible = viewerDistanceFromNearestEdge <= maxViewDistance; // check if the terrain chunk should be visible

				if (visible) {
					int lodIndex = 0;

					for (int i = 0; i < detailLevels.Length - 1; i++) {
						if (viewerDistanceFromNearestEdge > detailLevels [i].visibleDistanceThreshold) { // determine which resolution to render the terrain chunk
							lodIndex = i + 1;
						} else {
							break;
						}
					}

					if (lodIndex != previousLODIndex) { // if the resolution is different from the last one used to render this chunk
						LODMesh lodMesh = lodMeshes [lodIndex];
						if (lodMesh.hasMesh) {
							previousLODIndex = lodIndex;
							meshFilter.mesh = lodMesh.mesh;
						} else if (!lodMesh.hasRequestedMesh) { // if the mesh has not received its mesh data yet
							lodMesh.RequestMesh (mapData);
						}
					}

					if (lodIndex == 0) {
						if (collisionLODMesh.hasMesh) {
							meshCollider.sharedMesh = collisionLODMesh.mesh;
						} else if (!collisionLODMesh.hasRequestedMesh) {
							collisionLODMesh.RequestMesh (mapData);
						}
					}

					terrainChunksVisibleLastUpdate.Add (this);
				}

				SetVisible (visible);
			}
		}

		public void SetVisible (bool visible) {
			meshObject.SetActive (visible);
		}

		public bool IsVisible () {
			return meshObject.activeSelf;
		}

	}

	class LODMesh {

		public Mesh mesh;
		public bool hasRequestedMesh;
		public bool hasMesh;
		int lod;
		System.Action updateCallback;

		public LODMesh (int lod, System.Action updateCallback) {
			this.lod = lod;
			this.updateCallback = updateCallback;
		}

		// waits for mesh data to finish generating on separate thread
		void OnMeshDataReceived (MeshData meshData) {
			mesh = meshData.CreateMesh ();
			hasMesh = true;

			updateCallback ();
		}

		public void RequestMesh (MapData mapData) {
			hasRequestedMesh = true;
			mapGenerator.RequestMeshData (mapData, lod, OnMeshDataReceived);
		}

	}

	// inspector set values for visibility and resolution
	[System.Serializable]
	public struct LODInfo {
		public int lod;
		public float visibleDistanceThreshold;
		public bool useForCollider;
	}

}