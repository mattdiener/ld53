using Godot;

public partial class Item : RigidBody3D
{
	[Export]
	Node3D carrotRoot;

	[Export]
	Node3D watermelonRoot;

	[Export]
	Node3D appleRoot;

	public Node3D Parent { get {return parent;} set {
		parent = value;
		CustomIntegrator = (parent != null);
		if (parent != null) {
			parentInitialTransformInv = parent.GlobalTransform.Inverse();
		}
	}}
	Node3D parent = null;
	Transform3D parentInitialTransformInv;

	enum ItemType {
		Carrot,
		Watermelon,
		Apple,
		Count,
	}

	public string ItemName;

	private ItemType itemType;

	public void InitRandom(RandomNumberGenerator rng) {
		ItemType type = (ItemType)rng.RandiRange(0, (int)ItemType.Count-1);
		itemType = type;
		ItemName = type.ToString();
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		carrotRoot.Visible = false;
		watermelonRoot.Visible = false;
		appleRoot.Visible = false;

		switch(itemType) {
			case ItemType.Carrot:
				carrotRoot.Visible = true;
				break;
			case ItemType.Watermelon:
				watermelonRoot.Visible = true;
				break;
			case ItemType.Apple:
				appleRoot.Visible = true;
				break;
		}
	}

	public override void _IntegrateForces(PhysicsDirectBodyState3D state) {
		if (parent == null) {
			base._IntegrateForces(state);
			return;
		}
		var deltaP = (parent.GlobalPosition - GlobalPosition) * Engine.PhysicsTicksPerSecond * 0.95f;
		var deltaA = (parent.GlobalRotation - GlobalRotation) * Engine.PhysicsTicksPerSecond * 0.95f;
		state.LinearVelocity = deltaP;
		state.AngularVelocity = deltaA;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
