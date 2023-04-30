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
	CollisionShape3D collisionShape;

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

	bool isRunning = false;
	bool justJumped = false;
	bool beginJump = false;

	int floorCount = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		animationPlayer.CurrentAnimation = "idle";
		floorDetector.BodyEntered += enterFloor;
		floorDetector.BodyExited += exitFloor;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ResetFrameState();
		ProcessWalk();
		ProcessLeanAndRotate();
		ProcessJump();
		ProcessCamera();
		PickAnimation();
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
		}

		if (beginJump) {
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
		if (Input.IsActionJustPressed("jump")) {
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

	void ProcessCamera() {
		Vector3 vForward = forward.GlobalPosition - GlobalPosition;
		Vector3 forwardXZ = new(vForward.X, 0, vForward.Z);
		if (forwardXZ != Vector3.Zero) {
			cameraOnPath.GlobalPosition = GlobalPosition - (forwardXZ * 2.0f) + Vector3.Up;
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

	bool IsOnGround() {
		return floorCount > 0;
	}

	bool IsUpright() {
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
