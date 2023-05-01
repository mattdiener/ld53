using Godot;
using System;

public partial class HudNode : PanelContainer
{
	[Export]
	Label nameLabel;

	[Export]
	Label distanceLabel;

	[Export]
	CanvasItem pickupIndicator;

	Tween t;

	public float Distance{ set{ distanceLabel.Text = value.ToString("0.00") + "m";}}
	public string ItemName{ set{ nameLabel.Text = value; }}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void ShowCanPickup() {
		if (t?.IsRunning() == true) {
			t.Pause();
		}
		t = CreateTween();
		t.TweenProperty(pickupIndicator, "modulate", new Color(1.0f, 1.0f, 1.0f, 1.0f), 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
	}

	public void HideCanPickup() {
		if (t?.IsRunning() == true) {
			t.Pause();
		}
		t = CreateTween();
		t.TweenProperty(pickupIndicator, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
	}
}
