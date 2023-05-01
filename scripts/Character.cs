using System.Collections.Generic;
using Godot;

public partial class Character : RigidBody3D
{
	[Export]
	Path3D cameraOnPath;

	[Export]
	PathFollow3D cameraPath;

	[Export]
	Area3D cameraCollision;

	[Export]
	RayCast3D cameraCast;

	[Export]
	float jumpStrength = 12.0f;

	[Export]
	AudioStreamPlayer yepPlayer;

	[Export]
	AudioStreamPlayer oofPlayer;

	[Export]
	Node3D forward;

	[Export]
	Node3D right;

	[Export]
	Node3D up;

	[Export]
	Node3D walkCenter;

	[Export]
	AnimationPlayer animationPlayer;

	[Export]
	Area3D floorDetector;

	[Export]
	Game game;

	[Export]
	CollisionShape3D collisionShape;

	List<MountPoint> mountPoints;
	List<Item> objectsOnMounts;

	[Export]
	Node3D worldRoot;

	[Export]
	float speed = 30.0f;

	[Export]
	float rotateSpeed = 5.0f;

	[Export]
	float leanSpeed = 15.0f;

	[Export]
	float cameraRotationSpeed = Mathf.Pi / 60.0f;

	[Export]
	float cameraMoveSpeed = 0.03f;

	[Export]
	float uprightThreshold = 0.8f;

	RandomNumberGenerator rand = new();
	public bool IsActive {get; set;}

	bool isRunning;
	bool justJumped;
	bool beginJump;
	bool wasUpright;

	int floorCount = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		animationPlayer.CurrentAnimation = "idle";
		floorDetector.BodyEntered += enterFloor;
		floorDetector.BodyExited += exitFloor;

		InitMountPoints();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (!IsActive) {
			return;
		}

		ResetFrameState();
		ProcessFall();
		ProcessWalk();
		ProcessLeanAndRotate();
		ProcessJump();
		ProcessCamera();
		PickAnimation();
	}

	public bool CanHoldItem() {
		for (int i = 0; i < objectsOnMounts.Count; i++) {
			if (objectsOnMounts[i] == null) {
				return true;
			}
		}
		return false;
	}

	public void HoldItem(Item item) {
		List<int> freeIndexes = new();
		for (int i = 0; i < objectsOnMounts.Count; i++) {
			if (objectsOnMounts[i] == null) {
				freeIndexes.Add(i);
			}
		}
		if (freeIndexes.Count == 0) {
			GD.Print("All loaded up!");
			return;
		}
		int chosenIdx = freeIndexes[rand.RandiRange(0, freeIndexes.Count-1)];
		MountPoint chosenMount = mountPoints[chosenIdx];

		// Reparent
		Vector3 itemGlobalPosition = item.GlobalPosition;
		item.GetParent().RemoveChild(item);
		chosenMount.AddChild(item);
		item.GlobalPosition = itemGlobalPosition;

		objectsOnMounts[chosenIdx] = item;
		item.Freeze = true;
		item.CollisionLayer = 0;
		Tween t = CreateTween();
		t.TweenProperty(item, "position", new Vector3(0.0f, 0.0f, 0.0f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		t.TweenCallback(Callable.From(() => {
			Vector3 posBeforeReparent = item.GlobalPosition;
			chosenMount.RemoveChild(item);
			worldRoot.AddChild(item);
			item.GlobalPosition = posBeforeReparent;
			item.Freeze = false;
			item.CollisionLayer = 1;

			// possible that we fell in that half second
			if (objectsOnMounts[chosenIdx] != item) {
				return;
			}
			item.Parent = chosenMount;
		}));
	}

	void InitMountPoints() {
		mountPoints = new();
		objectsOnMounts = new();
		foreach (var mp in FindChildren("*")) {
			if (mp is MountPoint mount) {
				mountPoints.Add(mount);
				objectsOnMounts.Add(null);
			}
		}
	}

	void ResetFrameState() {
		isRunning = false;
		beginJump = false;
	}

	void PickAnimation() {
		if (justJumped) {
			if (animationPlayer.IsPlaying() && animationPlayer.CurrentAnimation == "jump") {
				return;
			}
			justJumped = false;
		}

		if (IsOnGround() && !IsUpright()) {
			animationPlayer.CurrentAnimation = "fallen";
			return;
		}

		if (beginJump) {
			yepPlayer.Play(0);
			animationPlayer.CurrentAnimation = "jump";
			justJumped = true;
			return;
		}

		if (isRunning) {
			animationPlayer.CurrentAnimation = "run";
			return;
		}

		animationPlayer.CurrentAnimation = "idle";
	}

	void ProcessFall() {
		if (IsUpright()) {
			wasUpright = true;
			return;
		}

		// In air means we haven't fallen.
		if (!IsOnGround()) {
			return;
		}

		// Now fallen
		if (wasUpright) {
			OnFall();
		}
		wasUpright = false;
	}

	void ProcessWalk() {
		if (!IsUpright() || !IsOnGround()) {
			return;
		}

		Vector3 vForwardP = forward.GlobalPosition - GlobalPosition;
		Vector3 vRightP = right.GlobalPosition - GlobalPosition;

		Vector3 vForward = new Vector3(vForwardP.X, 0.0f, vForwardP.Z).Normalized();
		Vector3 vRight = new Vector3(vRightP.X, 0.0f, vRightP.Z).Normalized();

		Vector3 dVelocity = new(0.0f, -0.2f, 0.0f);
		if (Input.IsActionPressed("walk_forward")) {
			dVelocity += vForward;
			isRunning = true;
		}
		if (Input.IsActionPressed("walk_backward")) {
			dVelocity -= vForward;
			isRunning = true;
		} 
		if (Input.IsActionPressed("walk_left")) {
			dVelocity -= vRight;
			isRunning = true;
		} 
		if (Input.IsActionPressed("walk_right")) {
			dVelocity += vRight;
			isRunning = true;
		}
		float realSpeed = Uprightness() * speed;
		ApplyForce(dVelocity.Normalized() * realSpeed * Mass, walkCenter.GlobalPosition - GlobalPosition);
	}

	void ProcessJump() {
		if (!IsUpright() || !IsOnGround()) {
			return;
		}

		Vector3 vUp = up.GlobalPosition - GlobalPosition;
		if (Input.IsActionJustPressed("jump") && !justJumped) {
			ApplyCentralImpulse(vUp * Mass * jumpStrength);
			beginJump = true;
		}
	}

	void ProcessLeanAndRotate() {
		Vector3 vForward = forward.GlobalPosition - GlobalPosition;
		Vector3 vRight = right.GlobalPosition - GlobalPosition;
		Vector3 vUp = up.GlobalPosition - GlobalPosition;

		Vector3 dLeanAxis = Vector3.Zero;
		if (Input.IsActionPressed("lean_forward")) {
			dLeanAxis -= vRight;
		}
		if (Input.IsActionPressed("lean_backward")) {
			dLeanAxis += vRight;
		} 
		if (Input.IsActionPressed("lean_left")) {
			dLeanAxis -= vForward;
		} 
		if (Input.IsActionPressed("lean_right")) {
			dLeanAxis += vForward;
		}
		ApplyTorque(dLeanAxis.Normalized() * leanSpeed * Mass);

		Vector3 dRotateAxis = Vector3.Zero;
		if (Input.IsActionPressed("rotate_left")) {
			dRotateAxis += vUp;
		} 
		if (Input.IsActionPressed("rotate_right")) {
			dRotateAxis -= vUp;
		}
		ApplyTorque(dRotateAxis.Normalized() * rotateSpeed * Mass);

	}

	void OnFall() {
		oofPlayer.Play(0);
		List<Item> toDetach = new();
		for (int i = 0; i < objectsOnMounts.Count; i++) {
			if (objectsOnMounts[i] != null) {
				toDetach.Add(objectsOnMounts[i]);
				objectsOnMounts[i] = null;
			}
		}
		game.ReturnToWorld(toDetach);
	}

	public void TransferItemsToBox(DeliveryBox box, Callable callback) {
		List<Item> toDeposit = new();
		for (int i = 0; i < objectsOnMounts.Count; i++) {
			if (objectsOnMounts[i] != null) {
				toDeposit.Add(objectsOnMounts[i]);
				objectsOnMounts[i] = null;
			}
		}
		box.Deposit(toDeposit, callback);
	}

	public int CountItems() {
		int count = 0;
		for (int i = 0; i < objectsOnMounts.Count; i++) {
			if (objectsOnMounts[i] != null) {
				count++;
			}
		}
		return count;
	}

	void ProcessCamera() {
		Vector3 vForward = forward.GlobalPosition - GlobalPosition;
		Vector3 forwardXZ = new(vForward.X, 0, vForward.Z);
		if (forwardXZ != Vector3.Zero) {
			cameraOnPath.GlobalPosition = GlobalPosition - (forwardXZ * 2.0f) + (Vector3.Up * 1.5f);
			var angle = Mathf.Atan2(forwardXZ.X, forwardXZ.Z);
			cameraOnPath.Rotation = RotateTowards(cameraOnPath.Rotation, new(0, angle, 0));
		}

		var castCollide = cameraCast.GetCollider();
		if (castCollide != null && castCollide != this) {
			if (cameraPath.ProgressRatio < (1.0f - cameraMoveSpeed)) {
				cameraPath.ProgressRatio += cameraMoveSpeed;
			}
		} else {
			if (cameraPath.ProgressRatio > (0.0f + cameraMoveSpeed)) {
				cameraPath.ProgressRatio -= cameraMoveSpeed;
			}
		}
	}

	// Assumes only y component.
	Vector3 RotateTowards(Vector3 oldRotation, Vector3 target) {
		float targetY = target.Y;
		float oldY = oldRotation.Y;
		float deltaY = targetY - oldY;	

		deltaY = Mathf.Wrap(deltaY, -Mathf.Pi, Mathf.Pi);
		if (Mathf.Abs(deltaY) <= cameraRotationSpeed) {
			return new Vector3(0, targetY, 0);
		}
		if (deltaY < 0) {
			return new Vector3(0, oldY-cameraRotationSpeed, 0);
		}
		return new Vector3(0, oldY+cameraRotationSpeed, 0);
	}

	public void AppendCameraTween(Tween t) {
		Vector3 vForward = forward.GlobalPosition - GlobalPosition;
		Vector3 forwardXZ = new(vForward.X, 0, vForward.Z);
		var angle = Mathf.Atan2(forwardXZ.X, forwardXZ.Z);
		t.Parallel().TweenProperty(cameraOnPath, "position", GlobalPosition - (forwardXZ * 2.0f) + Vector3.Up, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		t.Parallel().TweenProperty(cameraOnPath, "rotation", new Vector3(0.0f, angle, 0.0f), 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
	}

	bool IsOnGround() {
		return floorCount > 0;
	}

	public bool IsUpright() {
		return (up.GlobalPosition - GlobalPosition).Y >= uprightThreshold;
	}

	float Uprightness() {
		return ((up.GlobalPosition - GlobalPosition).Y - uprightThreshold) / (up.Position.Y - uprightThreshold);
	}

	void enterFloor(Node3D _) {
		floorCount++;
	}

	void exitFloor(Node3D _) {
		floorCount--;
	}
}
