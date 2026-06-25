using Godot;

/// <summary>
/// Câmera orbital em 3ª pessoa com:
///   - Scroll do mouse: zoom in/out
///   - Tecla C: alternar 1ª / 3ª pessoa
///   - ESC: soltar mouse | Click esquerdo: recapturar
/// </summary>
public partial class CameraFollow : Camera3D
{
	[Export] public float Distance      = 6.0f;
	[Export] public float MinDistance   = 1.5f;
	[Export] public float MaxDistance   = 25.0f;
	[Export] public float ScrollStep    = 0.8f;
	[Export] public float HeightOffset  = 0.4f;
	[Export] public float MouseSensX    = 0.003f;
	[Export] public float MouseSensY    = 0.003f;
	[Export] public float FollowSpeed   = 8.0f;
	[Export] public float MinPitch      = -1.10f;
	[Export] public float MaxPitch      =  0.05f;

	private Node3D _target;   // torso (group: camera_target)
	private Node3D _head;     // cabeça (group: player_head) — 1ª pessoa
	private float  _yaw      =  0f;
	private float  _pitch    = -0.45f;
	private bool   _firstPerson = false;

	public override void _Ready()
	{
		AddToGroup("main_camera");
		LocateNodes();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	// ── Rotação de mouse: _Input para chegar mesmo quando UI está na frente ───
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion
			&& Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yaw   -= motion.Relative.X * MouseSensX;
			_pitch -= motion.Relative.Y * MouseSensY;
			_pitch  = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
			GetViewport().SetInputAsHandled();
		}
	}

	// ── Zoom e teclas: _UnhandledInput (não consumido por Control/UI) ─────────
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			switch (mb.ButtonIndex)
			{
				case MouseButton.WheelUp:
					Distance = Mathf.Max(MinDistance, Distance - ScrollStep);
					GetViewport().SetInputAsHandled();
					break;
				case MouseButton.WheelDown:
					Distance = Mathf.Min(MaxDistance, Distance + ScrollStep);
					GetViewport().SetInputAsHandled();
					break;
				case MouseButton.Left:
					if (Input.MouseMode == Input.MouseModeEnum.Visible)
					{
						Input.MouseMode = Input.MouseModeEnum.Captured;
						GetViewport().SetInputAsHandled();
					}
					break;
			}
		}

		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			switch (key.Keycode)
			{
				case Key.C:
					_firstPerson = !_firstPerson;
					GetViewport().SetInputAsHandled();
					break;
				case Key.Escape:
					Input.MouseMode = Input.MouseModeEnum.Visible;
					GetViewport().SetInputAsHandled();
					break;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_target == null || !IsInstanceValid(_target))
			LocateNodes();
		if (_target == null) return;

		if (_firstPerson)
			UpdateFirstPerson();
		else
			UpdateThirdPerson((float)delta);
	}

	// ── 3ª pessoa ─────────────────────────────────────────────────────────────

	private void UpdateThirdPerson(float delta)
	{
		Vector3 pivot = _target.GlobalPosition + Vector3.Up * HeightOffset;

		Vector3 offset = new Vector3(
			Mathf.Sin(_yaw)  * Mathf.Cos(_pitch),
			Mathf.Sin(-_pitch),
			Mathf.Cos(_yaw)  * Mathf.Cos(_pitch)
		) * Distance;

		GlobalPosition = GlobalPosition.Lerp(pivot + offset, delta * FollowSpeed);
		LookAt(pivot, Vector3.Up);
	}

	// ── 1ª pessoa ─────────────────────────────────────────────────────────────

	private void UpdateFirstPerson()
	{
		Node3D eye = (_head != null && IsInstanceValid(_head)) ? _head : _target;
		GlobalPosition = eye.GlobalPosition + Vector3.Up * 0.05f;
		GlobalRotation = new Vector3(_pitch, _yaw, 0f);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	private void LocateNodes()
	{
		var targets = GetTree().GetNodesInGroup("camera_target");
		if (targets.Count > 0) _target = targets[0] as Node3D;

		var heads = GetTree().GetNodesInGroup("player_head");
		if (heads.Count > 0) _head = heads[0] as Node3D;
	}
}
