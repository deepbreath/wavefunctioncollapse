﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class MapGenerator : MonoBehaviour {
	public const float BlockSize = 2f;

	public Vector3 MapSize;

	[HideInInspector]
	public Module[] Modules;

	// TODO serialize this
	public Slot[, ,] Map;

	[HideInInspector]
	public int SizeX;
	[HideInInspector]
	public int SizeY;
	[HideInInspector]
	public int SizeZ;

	public int SlotsFilled = 0;

	public Slot LatestFilled;

	public int UpConnector;
	public int DownConnector;

	public bool BorderConstraints = true;
	public bool AllowExclusions = true;

	public bool BuildOnCollapse;

	public IEnumerable<Slot> FlatMap {
		get {
			for (int y = this.SizeY - 1; y >= 0; y--) {
				for (int x = 0; x < this.SizeX; x++) {
					for (int z = 0; z < this.SizeZ; z++) {
						yield return this.Map[x, y, z];
					}
				}
			}
		}
	}

	private void createModules() {
		this.Modules = ModulePrototype.CreateModules(this).ToArray();
	}

	public void Generate() {
		this.destroyChildren();
		this.createModules();

		this.SizeX = (int)this.MapSize.x;
		this.SizeY = (int)this.MapSize.y;
		this.SizeZ = (int)this.MapSize.z;
		this.Map = new Slot[SizeX, SizeY, SizeZ];

		for (int x = 0; x < this.SizeX; x++) {
			for (int y = 0; y < this.SizeY; y++) {
				for (int z = 0; z < this.SizeZ; z++) {
					this.Map[x, y, z] = new Slot(new Vector3i(x, y, z), this);
				}
			}
		}

		var slotNeighboursInitialState = new int[6][];
		for (int i = 0; i < 6; i++) {
			slotNeighboursInitialState[i] = new int[this.Modules.Length];
			foreach (var module in this.Modules) {
				foreach (int possibleNeighbour in module.PossibleNeighbours[i]) {
					slotNeighboursInitialState[i][possibleNeighbour]++;
				}
			}
		}

		for (int d = 0; d < 6; d++) {
			for (int i = 0; i < this.Modules.Count(); i++) {
				if (slotNeighboursInitialState[d][i] == 0) {
					throw new Exception("Module " + this.Modules[i].Prototype.name + " cannot be reached from direction " + d + " (" + this.Modules[i].Prototype.Faces[d].ToString() + ")!");
				}
			}
		}

		foreach (var slot in this.FlatMap) {
			slot.InitializeNeighbours();
			slot.NeighborCandidateHealth = slotNeighboursInitialState.Select(a => a.ToArray()).ToArray();
		}

		if (this.BorderConstraints) {
			for (int x = 0; x < this.SizeX; x++) {
				for (int z = 0; z < this.SizeZ; z++) {
					this.Map[x, this.SizeY - 1, z].EnforeConnector(4, this.UpConnector);
					this.Map[x, 0, z].EnforeConnector(1, this.DownConnector);
				}
			}
		}

		this.SlotsFilled = 0;
		int total = this.SizeX * this.SizeY * this.SizeZ;

		while (this.SlotsFilled < total) {
			this.Collapse();
		}
	}

	public void Collapse() {
		int minEntropy = this.FlatMap.Where(slot => !slot.Collapsed).Min(slot => slot.Entropy);
		var candidates = this.FlatMap.Where(slot => !slot.Collapsed && slot.Entropy == minEntropy).ToList();
		if (minEntropy == 0) {
			throw new Exception("Wavefunction collapse failed.");
		}
		
		int index = UnityEngine.Random.Range(0, candidates.Count);
		candidates[index].CollapseRandom();
	}

	public Vector3 GetWorldspacePosition(int x, int y, int z) {
		return this.transform.position
			+ Vector3.up * MapGenerator.BlockSize / 2f
			+ new Vector3(
				(float)(x) * MapGenerator.BlockSize,
				(float)(y) * MapGenerator.BlockSize,
				(float)(z) * MapGenerator.BlockSize);
	}

	private void destroyChildren() {
		var children = new List<Transform>();
		foreach (Transform child in this.transform) {
			children.Add(child);
		}
		foreach (var child in children) {
			GameObject.DestroyImmediate(child.gameObject);
		}
	}

	public void EnforceWalkway(Vector3i start, int direction) {
		var slot = this.Map[start.X, start.Y, start.Z];
		var toRemove = slot.Modules.Where(i => !this.Modules[i].GetFace(direction).Walkable).ToList();
		slot.RemoveModules(toRemove);
	}

	public void EnforceWalkway(Vector3i start, Vector3i destination) {
		int direction = Orientations.Get((destination - start).ToVector3());
		this.EnforceWalkway(start, direction);
		this.EnforceWalkway(destination, (direction + 3) % 6);
	}

	public void Build() {
		foreach (var slot in this.FlatMap) {
			slot.Build();
		}
	}
}