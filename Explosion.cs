using Godot;
using Destruct3D;

public partial class Explosion : Node3D
{
	private VSTSplittingComponent _explosion;

	public override void _Ready()
	{
		_explosion = GetNode<VSTSplittingComponent>("VSTSplittingComponent");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			_explosion.Activate();
		}
	}
}
