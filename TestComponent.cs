using Godot;

namespace MyCoolGame;

public partial class TestComponent : Component<Node2D>
{
	[DefaultValue<float>(1.0f)]
	[Export] public float Speed { get; set; } = 1.0f;

	public override void _Process(double delta)
	{
		var x = (Time.GetTicksMsec() / 1000f) * Speed;
		Host.Position = Vector2.FromAngle(x) * 100f;
	}
}

