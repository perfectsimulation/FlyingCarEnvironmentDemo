using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour {

	public enum DrawMode {  // editor preview
		NoiseMap,
		ColorMap,
		Mesh,
		FalloffMap
	};
	
	public DrawMode drawMode;

	public Noise.NormalizeMode normalizeMode; // set to local or global for max peak height

	public const int mapChunkSize = 239; // choose this chunk size to stay within the limit of largest mesh size (# vertices) possible in Unity
	[Range (0, 6)]
	public int editorPreviewLOD;
	public float noiseScale; // scale the noise values (how high the peaks get)

	public int octaves; // layers of noise; each subsequent layer is more granular
	[Range (0, 1)]
	public float persistance; // decrease in amplitude of each octave-- how much small features influence overall shape of map
	public float lacunarity; // increase in frequency of each octave-- increase number of small features

	public int seed; // keeps info for specific random generator values
	public Vector2 offset;

	public bool useFalloff; // make island

	public float meshHeightMultiplier;
	public AnimationCurve meshHeightCurve; // use this to make water flat

	public bool autoUpdate; // autoUpdate when new params are set in inspector

	public TerrainType [] regions; // use this to color and name the terrain types

	float [,] falloffMap;

	Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>> ();
	Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>> ();

	void Awake() {
		falloffMap = FalloffGenerator.GenerateFalloffMap (mapChunkSize);
	}

	void Update () {
		if (mapDataThreadInfoQueue.Count > 0) { // if there are new map data in queue
			for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
				MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}

		if (meshDataThreadInfoQueue.Count > 0) {
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
				MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}
	}

	public void DrawMapInEditor() {
		MapData mapData = GenerateMapData (Vector2.zero);

		MapDisplay display = FindObjectOfType<MapDisplay> ();
		if (drawMode == DrawMode.NoiseMap) {
			display.DrawTexture (TextureGenerator.TextureFromHeightMap (mapData.heightMap));
		} else if (drawMode == DrawMode.ColorMap) {
			display.DrawTexture (TextureGenerator.TextureFromColorMap (mapData.colorMap, mapChunkSize, mapChunkSize));
		} else if (drawMode == DrawMode.Mesh) {
			display.DrawMesh (MeshGenerator.GenerateTerrainMesh (mapData.heightMap, meshHeightMultiplier, meshHeightCurve, editorPreviewLOD), TextureGenerator.TextureFromColorMap (mapData.colorMap, mapChunkSize, mapChunkSize));
		} else if (drawMode == DrawMode.FalloffMap) {
			display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
		}
	}

	// spreads map generation calculations across multiple frames
	public void RequestMapData(Vector2 center, Action<MapData> callback) {
		ThreadStart threadStart = delegate {
			MapDataThread (center, callback);
		};

		new Thread (threadStart).Start ();
	}

	void MapDataThread (Vector2 center, Action<MapData> callback) {
		MapData mapData = GenerateMapData (center); // executed on this thread
		lock (mapDataThreadInfoQueue) { // lock queue so only access it on one thread at a time
			mapDataThreadInfoQueue.Enqueue (new MapThreadInfo<MapData> (callback, mapData));
		}
	}

	// spreads mesh generation calculations across multiple frames
	public void RequestMeshData (MapData mapData, int lod, Action<MeshData> callback) {
		ThreadStart threadStart = delegate {
			MeshDataThread (mapData, lod, callback);
		};

		new Thread (threadStart).Start ();
	}

	void MeshDataThread (MapData mapData, int lod, Action<MeshData> callback) {
		MeshData meshData = MeshGenerator.GenerateTerrainMesh (mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod);
		lock (meshDataThreadInfoQueue) {
			meshDataThreadInfoQueue.Enqueue (new MapThreadInfo<MeshData> (callback, meshData));
		}
	}

	// called on its own thread to generate map data for a new terrain chunk
	MapData GenerateMapData (Vector2 center) {
		float [,] noiseMap = Noise.GenerateNoiseMap (mapChunkSize + 2, mapChunkSize + 2, seed, noiseScale, octaves, persistance, lacunarity, center + offset, normalizeMode);

		Color [] colorMap = new Color [mapChunkSize * mapChunkSize];
		for (int y = 0; y < mapChunkSize; y++) {
			for (int x = 0; x < mapChunkSize; x++) {
				if (useFalloff) {
					noiseMap [x, y] = Mathf.Clamp01 (noiseMap [x, y] - falloffMap [x, y]);
				}
				float currentHeight = noiseMap [x, y];
				for (int i = 0; i < regions.Length; i++) {
					if (currentHeight >= regions [i].height) {
						colorMap [y * mapChunkSize + x] = regions [i].color;
					} else {
						break;
					}
				}
			}
		}
			
		return new MapData (noiseMap, colorMap);
	}

	// ensures inspector input makes sense
	void OnValidate () {
		if (lacunarity < 1) {
			lacunarity = 1;
		}
		if (octaves < 0) {
			octaves = 0;
		}

		falloffMap = FalloffGenerator.GenerateFalloffMap (mapChunkSize);
	}

	// handles map data and mesh data
	struct MapThreadInfo<T> {
		public readonly Action<T> callback;
		public readonly T parameter;

		public MapThreadInfo (Action<T> callback, T parameter) {
			this.callback = callback;
			this.parameter = parameter;
		}
	}

}

[System.Serializable]
public struct TerrainType {
	public string name;
	public float height;
	public Color color;
}

public struct MapData {
	public readonly float [,] heightMap;
	public readonly Color [] colorMap;

	public MapData (float [,] heightMap, Color [] colorMap) {
		this.heightMap = heightMap;
		this.colorMap = colorMap;
	}
}