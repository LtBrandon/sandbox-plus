[Library( "ent_thruster", Title = "Thruster" )]
public partial class ThrusterComponent : BaseWireInputComponent, Component.IPressable, IDuplicatable
{
	[Property, Sync] public float ForceMultiplier { get; set; } = 100.0f;
	[Property] public float Force = 0f;
	[Property] public bool IsForward { get; set; } = true;
	[Property, Sync] public bool Massless { get; set; } = true;

	// can't sync a rigidbody, so sync its GameObject instead
	[Property, Sync] public GameObject TargetObject { get; set; }
	[Property] public Rigidbody TargetBody { get; set; }
	[Property] public GameObject OnEffect { get; set; }

	protected bool On
	{
		get
		{
			return !Force.AlmostEqual( 0 );
		}
		set
		{
			Force = value ? 1 : 0;
			if ( !Force.AlmostEqual( 0 ) )
			{
				OnThrusterEnabled();
			}
			else
			{
				OnThrusterDisabled();
			}
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		OnEffect ??= GameObject.Clone("entities/thruster/fx_thruster.prefab", new CloneConfig { Parent = GameObject, StartEnabled = false });
		OnEffect?.Enabled = false;
	}

	protected void OnThrusterEnabled()
	{
		// Turn emitter on
		OnEffect?.Enabled = true;
	}

	protected void OnThrusterDisabled()
	{
		// Turn emitter off
		OnEffect?.Enabled = false;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		return true;
	}
	[Rpc.Broadcast]
	public void Press( GameObject presser )
	{
		if ( presser.Network.Owner != Rpc.Caller )
			return;

		On = !On;
		Sound.Play( On ? "drop_001" : "drop_002", WorldPosition );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		Press( e.Source.GameObject );
		return true;
	}

	public override void WireInitialize()
	{
		this.RegisterInputHandler( "Force", ( float value ) =>
		{
			this.Force = value;
			if ( !Force.AlmostEqual( 0 ) )
			{
				OnThrusterEnabled();
			}
			else
			{
				OnThrusterDisabled();
			}
		} );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Force.AlmostEqual( 0 ) )
		{
			if ( !TargetObject.IsValid() )
			{
				TargetBody = TargetObject.GetComponent<Rigidbody>();
			}
			if ( !TargetBody.IsValid() )
			{
				TargetObject = GameObject;
				TargetBody = GetComponent<Rigidbody>();
				On = false;
			}
			TargetBody.ApplyForceAt(
				WorldPosition,
				GameObject.WorldRotation.Down * (
					Massless ?
						Force * ForceMultiplier * (TargetBody.Mass + GetComponent<Rigidbody>().PhysicsBody.Mass) * (IsForward ? 1 : -1)
						:
						Force * ForceMultiplier * (IsForward ? 1 : -1)
				)
			);
			if ( OnEffect.IsValid() )
			{
				var bounds = GetComponent<Prop>().Model.Bounds;
				OnEffect.WorldPosition = Transform.World.PointToWorld( bounds.Center + new Vector3( 0, 0, bounds.Extents.z ) );
			}
		}
	}

	Dictionary<string, object> IDuplicatable.PreDuplicatorCopy()
	{
		return new()
		{
			{ "Force", Force },
			{ "IsForward", IsForward },
			{ "Massless", Massless },
			{ "ForceMultiplier", ForceMultiplier },
			{ "TargetObject", TargetObject.Id.GetHashCode() },
		};
	}
	void IDuplicatable.PostDuplicatorPasteEntities( Dictionary<int, GameObject> entities, Dictionary<string, object> data )
	{
		Force = (float)data["Force"];
		if ( !Force.AlmostEqual( 0 ) )
		{
			OnThrusterEnabled();
		}
		IsForward = (bool)data["IsForward"];
		Massless = (bool)data["Massless"];
		ForceMultiplier = (float)data["ForceMultiplier"];
		if ( data.TryGetValue( "TargetBody", out var targetObject ) )
		{
			TargetObject = entities[(int)targetObject];
			TargetBody = TargetObject?.GetComponent<Rigidbody>();
		}
	}
}
