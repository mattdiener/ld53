using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node
{
	[Export]
	Control menu;

	[Export]
	Character character;

	[Export]
	DeliveryBox deliveryBox;

	[Export]
	PackedScene itemScene;

	[Export]
	PackedScene hudNodeScene;

	[Export]
	Node3D spawnerRoot;

	[Export]
	Node3D worldRoot;

	[Export]
	Control hudRoot;

	[Export]
	Camera3D camera;

	[Export]
	float closeThreshold = 2.0f;

	[Export]
	AudioStreamPlayer dingPlayer;

	int currentInteractibleItemIndex = -1;

	List<Node3D> spawners = new();
	List<Item> activeItemsInWorld = new();
	List<HudNode> hudNodes = new();

	HudNode boxHudNode;

	RandomNumberGenerator rng = new();

	bool started;
	bool showingBoxCanDeposit;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		spawners = new();
		activeItemsInWorld = new();
		hudNodes = new();

		InitSpawners();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		UpdateHud();
		UpdateInteractions();
	}

	void Start() {
		if (started) {
			return;
		}
		started = true;
		Tween t = CreateTween();
		t.Parallel().TweenProperty(menu, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		character.AppendCameraTween(t);
		t.TweenCallback(new(this, MethodName.UnfreezeCharacter));
		t.TweenCallback(Callable.From(() => {
			for (int i = 0; i < 4; i++) {
				SpawnItem();
			}
		}));
	}

	void UnfreezeCharacter() {
		character.Freeze = false;
		character.IsActive = true;
	}

	void InitSpawners() {
		foreach (var spawner in spawnerRoot.FindChildren("*")) {
			if (spawner is Node3D s3d) {
				spawners.Add(s3d);
			}
		}
	}

	Vector3 RandomSpawnerPosition() {
		return spawners[rng.RandiRange(0, spawners.Count-1)].GlobalPosition;
	}

	void SpawnItem() {
		Item item = itemScene.Instantiate<Item>();
		item.InitRandom(rng);
		worldRoot.AddChild(item);
		item.GlobalPosition = RandomSpawnerPosition();
		activeItemsInWorld.Add(item);

		HudNode hn = hudNodeScene.Instantiate<HudNode>();
		hudRoot.AddChild(hn);
		hudNodes.Add(hn);
	}

	void UpdateHud() {
		UpdateBoxHud();
		for (int i = 0; i < activeItemsInWorld.Count; i++) {
			var item = activeItemsInWorld[i];
			var hudItem = hudNodes[i];
			var playerToItemDist = (item.GlobalPosition - character.GlobalPosition).Length();
			hudItem.ItemName = item.ItemName;
			hudItem.Distance = playerToItemDist;

			var cameraToItem = camera.GlobalTransform.Inverse() * item.GlobalPosition;
			cameraToItem.Z *= -1.0f;

			// in front of us
			if (cameraToItem.Z > 0) {
				hudItem.Position = camera.UnprojectPosition(item.GlobalPosition).Clamp(new(0,0), GetWindow().Size - hudItem.Size);
			} else {
				// Hacky way to get the thing on the edge of the screen.
				cameraToItem.Z = 0.000001f;
				var wSize = GetWindow().Size;
				var hToV = (float)wSize.Y / (float)wSize.X;

				// Angle we can go in each direction before off screen
				var halfHfov = Mathf.DegToRad(camera.Fov / 2.0f);
				var halfVfov = hToV * halfHfov;

				var hAngle = Mathf.Atan2(cameraToItem.X, cameraToItem.Z);
				var vAngle = -Mathf.Atan2(cameraToItem.Y, cameraToItem.Z);
				Vector2 hudPosition = new(hAngle, vAngle);
				hudPosition = hudPosition.Clamp(new(-halfHfov, -halfVfov), new(halfHfov, halfVfov));
				hudPosition += new Vector2(halfHfov, halfVfov);
				hudPosition /= new Vector2(halfHfov*2, halfVfov*2);
				hudPosition *= GetWindow().Size;
				hudPosition = hudPosition.Clamp(new(0,0), GetWindow().Size - hudItem.Size);

				hudItem.Position = hudPosition;
			}
		}
	}

	public void ReturnToWorld(List<Item> items) {
		foreach (var item in items) {
			activeItemsInWorld.Add(item);
			item.Parent = null;
			var hn = hudNodeScene.Instantiate<HudNode>();
			hudRoot.AddChild(hn);
			hudNodes.Add(hn);
		}
	}

	void UpdateBoxHud() {
		// bad copy pasta!
		if (character.CountItems() == 0) {
			if (boxHudNode != null) {
				boxHudNode.QueueFree();
				boxHudNode = null;
			}
			return;
		}

		if (boxHudNode == null) {
			var hn = hudNodeScene.Instantiate<HudNode>();
			hudRoot.AddChild(hn);
			boxHudNode = hn;
		}
		var item = deliveryBox;
		var hudItem = boxHudNode;
		var playerToItemDist = (item.GlobalPosition - character.GlobalPosition).Length();
		hudItem.ItemName = "Return";
		hudItem.Distance = playerToItemDist;

		var cameraToItem = camera.GlobalTransform.Inverse() * item.GlobalPosition;
		cameraToItem.Z *= -1.0f;

		// in front of us
		if (cameraToItem.Z > 0) {
			hudItem.Position = camera.UnprojectPosition(item.GlobalPosition).Clamp(new(0,0), GetWindow().Size - hudItem.Size);
		} else {
			// Hacky way to get the thing on the edge of the screen.
			cameraToItem.Z = 0.000001f;
			var wSize = GetWindow().Size;
			var hToV = (float)wSize.Y / (float)wSize.X;

			// Angle we can go in each direction before off screen
			var halfHfov = Mathf.DegToRad(camera.Fov / 2.0f);
			var halfVfov = hToV * halfHfov;

			var hAngle = Mathf.Atan2(cameraToItem.X, cameraToItem.Z);
			var vAngle = -Mathf.Atan2(cameraToItem.Y, cameraToItem.Z);
			Vector2 hudPosition = new(hAngle, vAngle);
			hudPosition = hudPosition.Clamp(new(-halfHfov, -halfVfov), new(halfHfov, halfVfov));
			hudPosition += new Vector2(halfHfov, halfVfov);
			hudPosition /= new Vector2(halfHfov*2, halfVfov*2);
			hudPosition *= GetWindow().Size;
			hudPosition = hudPosition.Clamp(new(0,0), GetWindow().Size - hudItem.Size);

			hudItem.Position = hudPosition;
		}
	}

	int GetClosestItemIndex() {
		float bestDistance = float.PositiveInfinity;
		int bestIndex = -1;
		for (int i = 0; i < activeItemsInWorld.Count; i++) {
			var dist = character.GlobalPosition.DistanceSquaredTo(activeItemsInWorld[i].GlobalPosition);
			if (dist < bestDistance) {
				bestDistance = dist;
				bestIndex = i;
			}
		}
		return bestIndex;
	}

	void UpdateInteractions() {
		int itemIdx = GetClosestItemIndex();
		Item item = null;
		if (itemIdx >= 0) {
			item = activeItemsInWorld[itemIdx];
		}
		
		var boxDistanceSq = (deliveryBox.GlobalPosition - character.GlobalPosition).LengthSquared();
		
		float closestItemDistanceSq;
		if (item == null) {
			closestItemDistanceSq = float.PositiveInfinity;
		} else {
			closestItemDistanceSq = (item.GlobalPosition - character.GlobalPosition).LengthSquared();
		}

		bool isNearestBox = false;
		bool isNearestItem = false;

		if (boxDistanceSq < closestItemDistanceSq && character.CountItems() > 0) {
			// box is closest and we have and item
			if (currentInteractibleItemIndex != -1) {
				hudNodes[itemIdx].HideCanPickup();
			}
			currentInteractibleItemIndex = -1;

			if (boxDistanceSq < (closeThreshold * closeThreshold)) {
				if (!showingBoxCanDeposit) {
					boxHudNode.ShowCanPickup();
					showingBoxCanDeposit = true;
				}
				isNearestBox = true;
			}
		} else if(itemIdx >= 0) {
			// Hide the last pickup if it's different from this one
			if (currentInteractibleItemIndex != -1 && currentInteractibleItemIndex != itemIdx) {
				hudNodes[currentInteractibleItemIndex].HideCanPickup();
			}

			// If we're close enough
			if (closestItemDistanceSq < (closeThreshold * closeThreshold) && character.CanHoldItem() && character.IsUpright()) {
				// and we weren't already showing interractible
				isNearestItem = true;
				if (currentInteractibleItemIndex != itemIdx) {
					hudNodes[itemIdx].ShowCanPickup();
				}
				currentInteractibleItemIndex = itemIdx;
			} else {
				// not close enough and we were showing interractible
				if (currentInteractibleItemIndex == itemIdx) {
					hudNodes[itemIdx].HideCanPickup();
				}
				currentInteractibleItemIndex = -1;
			}
		}

		if (!isNearestBox && showingBoxCanDeposit) {
			boxHudNode?.HideCanPickup();
			showingBoxCanDeposit = false;
		}

		if (Input.IsActionJustPressed("interact")) {
			if (isNearestBox) {
				var itemsDeposited = character.CountItems();
				character.TransferItemsToBox(deliveryBox, Callable.From(() => {
					// TODO add points
					dingPlayer.Play(0);

					for(int i = 0; i < itemsDeposited; i++) {
						SpawnItem();
					}
				}));
			}
			
			if (isNearestItem) {
				HudNode oldHudNode = hudNodes[itemIdx];
				currentInteractibleItemIndex = -1;
				activeItemsInWorld.RemoveAt(itemIdx);
				hudNodes.RemoveAt(itemIdx);

				oldHudNode.QueueFree();
				character.HoldItem(item);
			}
		}
	}
}
