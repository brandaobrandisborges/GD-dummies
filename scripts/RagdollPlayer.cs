using Godot;

/// <summary>
/// Personagem jogável com movimento sólido (CharacterBody3D) + ragdoll em física
/// (PhysicalBoneSimulator3D). Quando está de pé, o AnimationPlayer dirige a pose.
/// Quando leva um hit, physical bones tomam conta e o boneco cai.
///
/// Estrutura esperada na cena:
///   CharacterBody3D  ← este script
///   ├── CollisionShape3D   (cápsula — para colisão de movimento)
///   └── <cena do personagem importada>
///       └── Skeleton3D
///           ├── MeshInstance3D
///           ├── AnimationPlayer
///           └── PhysicalBoneSimulator3D
///               ├── PhysicalBone3D (Hips)
///               ├── PhysicalBone3D (Spine) ...
///               └── ...
///
/// O script localiza AnimationPlayer e PhysicalBoneSimulator3D automaticamente
/// pelo tipo — sem depender de caminhos fixos.
/// </summary>
public partial class RagdollPlayer : CharacterBody3D
{
	[ExportGroup("Movement")]
	[Export] public float MoveSpeed    = 4.5f;
	[Export] public float JumpVelocity = 9.0f;
	[Export] public float Gravity      = 20.0f;
	[Export] public float Acceleration = 12.0f;

	// ── animações esperadas no AnimationPlayer ────────────────────────────────
	// Ajusta esses nomes conforme o seu pack de animações (Mixamo, Quaternius, etc.)
	[ExportGroup("Animations")]
	[Export] public string AnimIdle   = "idle";
	[Export] public string AnimWalk   = "walk";
	[Export] public string AnimGetUp  = "get_up";   // pode ser "" se não tiver

	// ── referências (descobertas automaticamente no _Ready) ───────────────────
	private AnimationPlayer         _animPlayer;
	private PhysicalBoneSimulator3D _pbSim;
	private Camera3D                _cachedCamera;
	private bool                    _isRagdoll;

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		AddToGroup("player_ragdoll");
		AddToGroup("camera_target");   // câmera olha para este nó
		RegisterInputActions();

		// Busca recursiva pelos tipos — funciona independente da hierarquia importada
		_animPlayer = FindFirst<AnimationPlayer>(this);
		_pbSim      = FindFirst<PhysicalBoneSimulator3D>(this);

		if (_animPlayer == null)
			GD.PushWarning("[RagdollPlayer] AnimationPlayer não encontrado na hierarquia.");
		if (_pbSim == null)
			GD.PushWarning("[RagdollPlayer] PhysicalBoneSimulator3D não encontrado. " +
						   "Selecione o Skeleton3D no editor → clique direito → " +
						   "\"Create Physical Skeleton\".");

		// Garante que ragdoll começa desativado
		if (_pbSim != null) _pbSim.Active = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isRagdoll) return;   // física das bones cuida de tudo no modo ragdoll
		HandleMovement((float)delta);
	}

	// ── movimento ─────────────────────────────────────────────────────────────

	private void HandleMovement(float delta)
	{
		var vel = Velocity;

		// Gravidade
		if (!IsOnFloor())
			vel.Y -= Gravity * delta;

		// Pulo
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
			vel.Y = JumpVelocity;

		// Direção relativa à câmera
		var cam   = FindCamera();
		var fwd   = cam != null ? -cam.GlobalTransform.Basis.Z : -GlobalTransform.Basis.Z;
		var right = cam != null ?  cam.GlobalTransform.Basis.X :  GlobalTransform.Basis.X;
		fwd.Y   = 0; if (fwd.LengthSquared()   > 1e-4f) fwd   = fwd.Normalized();
		right.Y = 0; if (right.LengthSquared() > 1e-4f) right = right.Normalized();

		var dir = Vector3.Zero;
		if (Input.IsActionPressed("move_forward")) dir += fwd;
		if (Input.IsActionPressed("move_back"))    dir -= fwd;
		if (Input.IsActionPressed("move_right"))   dir += right;
		if (Input.IsActionPressed("move_left"))    dir -= right;

		bool moving = dir.LengthSquared() > 1e-4f;
		if (moving)
		{
			dir   = dir.Normalized();
			vel.X = Mathf.Lerp(vel.X, dir.X * MoveSpeed, Acceleration * delta);
			vel.Z = Mathf.Lerp(vel.Z, dir.Z * MoveSpeed, Acceleration * delta);

			// Vira o personagem para a direção do movimento
			var lookDir = new Vector3(dir.X, 0f, dir.Z);
			if (lookDir.LengthSquared() > 1e-4f)
				LookAt(GlobalPosition + lookDir, Vector3.Up);
		}
		else
		{
			vel.X = Mathf.Lerp(vel.X, 0f, Acceleration * delta);
			vel.Z = Mathf.Lerp(vel.Z, 0f, Acceleration * delta);
		}

		Velocity = vel;
		MoveAndSlide();

		// Animação
		PlayAnim(moving ? AnimWalk : AnimIdle);
	}

	// ── ragdoll ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Ativa o modo ragdoll. Passe um impulso em espaço-mundo (ex: direção da explosão).
	/// </summary>
	public void StartRagdoll(Vector3 impulse = default)
	{
		if (_isRagdoll || _pbSim == null) return;
		_isRagdoll = true;
		_pbSim.Active = true;

		if (impulse != default)
		{
			int count = 0;
			foreach (var child in _pbSim.GetChildren())
				if (child is PhysicalBone3D) count++;

			if (count > 0)
			{
				var share = impulse / count;
				foreach (var child in _pbSim.GetChildren())
					if (child is PhysicalBone3D bone)
						bone.ApplyCentralImpulse(share);
			}
		}
	}

	/// <summary>
	/// Desativa o modo ragdoll e volta a animar.
	/// </summary>
	public void StopRagdoll()
	{
		if (!_isRagdoll || _pbSim == null) return;
		_isRagdoll = false;
		_pbSim.Active = false;
		PlayAnim(AnimGetUp != "" ? AnimGetUp : AnimIdle);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	private void PlayAnim(string anim)
	{
		if (_animPlayer == null || anim == "") return;
		if (!_animPlayer.HasAnimation(anim)) return;
		if (_animPlayer.CurrentAnimation == anim && _animPlayer.IsPlaying()) return;
		_animPlayer.Play(anim);
	}

	private Camera3D FindCamera()
	{
		if (_cachedCamera != null && IsInstanceValid(_cachedCamera)) return _cachedCamera;
		var list = GetTree().GetNodesInGroup("main_camera");
		if (list.Count > 0) _cachedCamera = list[0] as Camera3D;
		return _cachedCamera;
	}

	/// <summary>Busca recursiva pelo primeiro nó do tipo T na subárvore de root.</summary>
	private static T FindFirst<T>(Node root) where T : Node
	{
		foreach (var child in root.GetChildren())
		{
			if (child is T hit) return hit;
			var found = FindFirst<T>(child);
			if (found != null) return found;
		}
		return null;
	}

	private static void RegisterInputActions()
	{
		EnsureAction("move_forward", Key.W);
		EnsureAction("move_back",    Key.S);
		EnsureAction("move_left",    Key.A);
		EnsureAction("move_right",   Key.D);
		EnsureAction("jump",         Key.Space);
	}

	private static void EnsureAction(string name, Key key)
	{
		if (InputMap.HasAction(name)) return;
		InputMap.AddAction(name);
		InputMap.ActionAddEvent(name, new InputEventKey { Keycode = key });
	}
}
