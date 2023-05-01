using System.Collections.Generic;
using Godot;

public partial class DeliveryBox : Node3D
{
	[Export]
	Node3D aboveBox;

	[Export]
	Node3D inBox;

	[Export]
	GpuParticles3D fireworks;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Deposit(List<Item> items, Callable callback) {
		Tween t = CreateTween();
		foreach (var item in items) {
			item.Freeze = true;
			item.GetParent().RemoveChild(item);
			AddChild(item);
			t.TweenProperty(item, "position", aboveBox.GlobalPosition,  0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			t.TweenCallback(new(this, "DoFireworks"));
			t.TweenProperty(item, "position", inBox.GlobalPosition,  0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		}
		t.TweenCallback(Callable.From(() => {
			foreach (var item in items) {
				RemoveChild(item);
				item.QueueFree();
			}
		}));
		t.TweenCallback(callback);
	}

	public void DoFireworks() {
		fireworks.Emitting = true;
	}
}
