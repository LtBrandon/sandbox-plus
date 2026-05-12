namespace SandboxPlus;

public partial class PropHelper
{
	public List<Joint> Joints { get; set; } = new();
	// These "make a constraint" functions are not directly RPC'd as components are replicated upon Network.Refresh(), but AddJointToList needs to be RPC'd so everyone can track the joint's existance
	public Sandbox.FixedJoint Weld( GameObject to, bool noCollide = true, int fromBone = -1, int toBone = -1 )
	{
		var fixedJoint = GetJointGameObject( this.GameObject, fromBone ).Components.Create<Sandbox.FixedJoint>();
		fixedJoint.Body = GetJointGameObject( to, toBone );
		fixedJoint.EnableCollision = !noCollide;
		fixedJoint.LinearDamping = 0;
		fixedJoint.LinearFrequency = 0;
		fixedJoint.AngularDamping = 0;
		fixedJoint.AngularFrequency = 0;
		fixedJoint.Network.Refresh();

		AddJointToList( fixedJoint );
		to.Components.Get<PropHelper>()?.AddJointToList( fixedJoint );

		return fixedJoint;
	}

	public Sandbox.HingeJoint Axis( GameObject to, Transform pivot, bool noCollide = true, int fromBone = -1, int toBone = -1, float friction = 0, float minAngle = -180, float maxAngle = 180 )
	{
		var goJoint = new GameObject
		{
			WorldPosition = pivot.Position,
			WorldRotation = pivot.Rotation,
			Tags = { "jointHelper" }
		};
		goJoint.SetParent( GetJointGameObject( this.GameObject, fromBone ) );
		goJoint.NetworkSpawn();

		var hingeJoint = goJoint.Components.Create<Sandbox.HingeJoint>();
		hingeJoint.Body = GetJointGameObject( to, toBone );
		hingeJoint.EnableCollision = !noCollide;
		hingeJoint.Friction = friction;
		if ( !minAngle.AlmostEqual( -180 ) ) hingeJoint.MinAngle = minAngle; // actually setting them to -180/180 doesn't let them smoothly rotate, and I'm unsure how to unset them...
		if ( !maxAngle.AlmostEqual( 180 ) ) hingeJoint.MaxAngle = maxAngle;
		// also has motor settings
		hingeJoint.Network.Refresh();

		AddJointToList( hingeJoint );
		to.Components.Get<PropHelper>()?.AddJointToList( hingeJoint );
		CheckCarAchievement( to.GetComponent<PropHelper>() );

		return hingeJoint;
	}

	public Sandbox.BallJoint BallSocket( GameObject to, Transform pivot, bool noCollide = true, int fromBone = -1, int toBone = -1, float friction = 0 )
	{
		var goJoint = new GameObject
		{
			WorldPosition = pivot.Position,
			WorldRotation = pivot.Rotation,
			Tags = { "jointHelper" }
		};
		goJoint.SetParent( GetJointGameObject( this.GameObject, fromBone ) );
		goJoint.NetworkSpawn();

		var ballJoint = goJoint.Components.Create<Sandbox.BallJoint>();
		ballJoint.Body = GetJointGameObject( to, toBone );
		ballJoint.EnableCollision = !noCollide;
		ballJoint.Friction = friction;
		// ballJoint.SwingLimit
		// ballJoint.TwistLimit
		ballJoint.Network.Refresh();

		AddJointToList( ballJoint );
		to.Components.Get<PropHelper>()?.AddJointToList( ballJoint );

		return ballJoint;
	}

	public Sandbox.SpringJoint Spring( GameObject to, Vector3 pos1, Vector3 pos2, SpringJoint.SpringForceMode forceMode, bool noCollide = true, int fromBone = -1, int toBone = -1, float min = 0, float max = 0, float frequency = 5f, float damping = 0.7f, bool visualRope = true )
	{
		var goJoint = new GameObject
		{
			WorldPosition = pos1,
			Tags = { "jointHelper" }
		};
		goJoint.SetParent( GetJointGameObject( this.GameObject, fromBone ) );
		goJoint.NetworkSpawn();

		var toJoint = new GameObject
		{
			WorldPosition = pos2,
			Tags = { "jointHelper" }
		};
		toJoint.SetParent( GetJointGameObject( to, toBone ) );
		toJoint.NetworkSpawn();

		var springJoint = goJoint.Components.Create<Sandbox.SpringJoint>();
		springJoint.Attachment = Joint.AttachmentMode.LocalFrames;
		springJoint.LocalFrame1 = goJoint.LocalTransform;
		springJoint.LocalFrame2 = toJoint.LocalTransform;
		springJoint.EnableCollision = !noCollide;
		springJoint.MinLength = min;
		springJoint.MaxLength = max;
		springJoint.Frequency = frequency;
		springJoint.Damping = damping;
		springJoint.Body = toJoint;
		springJoint.ForceMode = forceMode;
		springJoint.Network.Refresh();

		AddJointToList( springJoint );
		to.Components.Get<PropHelper>()?.AddJointToList( springJoint );

		if ( visualRope )
		{
			float slack = float.Clamp(max - pos1.Distance( pos2 ), 0, max);
			MakeVisualRope( goJoint, pos1, toJoint, pos2, min.AlmostEqual(max), slack, 1f );
		}

		var propHelper2 = to.GetComponent<PropHelper>();
		if ( propHelper2.IsValid() )
		{
			propHelper2.OnComponentDestroy += () =>
			{
				goJoint.Destroy();
			};
		}

		return springJoint;
	}

	public Sandbox.SpringJoint Rope( GameObject to, Vector3 pos1, Vector3 pos2, bool noCollide = true, int fromBone = -1, int toBone = -1, float min = 0, float max = 0, bool visualRope = true )
	{
		return Spring( to, pos1, pos2, SpringJoint.SpringForceMode.Pull, noCollide, fromBone, toBone, min, max, frequency: 0, damping: 0, visualRope: visualRope );
	}
	public Sandbox.PhysicsFilter NoCollide( GameObject to, int fromBone = -1, int toBone = -1 )
	{
		var go = new GameObject( GameObject, false, "no collide" );
		var joint = go.AddComponent<PhysicsFilter>();
		joint.Body = to;

		go.NetworkSpawn();
		
		return joint;
	}

	public Sandbox.SliderJoint Slider( GameObject to, Vector3 pos1, Vector3 pos2, bool noCollide = true, int fromBone = -1, int toBone = -1, float min = 0, float max = 0, float friction = 0, bool visualRope = true )
	{
		// Todo: this isn't right yet. Something about positions and rotations.

		var goFrom = GetJointGameObject( this.GameObject, fromBone );
		var goTo = GetJointGameObject( to, toBone );
		var goJoint = new GameObject
		{
			WorldPosition = pos1,
			WorldRotation = goFrom.WorldRotation,
			Tags = { "jointHelper" }
		};
		goJoint.SetParent( goFrom );
		goJoint.NetworkSpawn();

		var toJoint = new GameObject
		{
			WorldPosition = pos2,
			WorldRotation = goFrom.WorldRotation,
			// WorldRotation = Rotation.LookAt(pos2 - pos1),
			Tags = { "jointHelper" }
		};
		toJoint.SetParent( goTo );
		toJoint.NetworkSpawn();

		var sliderJoint = goJoint.Components.Create<Sandbox.SliderJoint>();
		sliderJoint.Attachment = Joint.AttachmentMode.LocalFrames;
		sliderJoint.LocalFrame1 = goJoint.LocalTransform;
		sliderJoint.LocalFrame2 = toJoint.LocalTransform;
		sliderJoint.EnableCollision = !noCollide;
		sliderJoint.MinLength = min;
		sliderJoint.MaxLength = max;
		sliderJoint.Friction = friction;
		sliderJoint.Body = toJoint;
		sliderJoint.Network.Refresh();

		AddJointToList( sliderJoint );
		to.Components.Get<PropHelper>()?.AddJointToList( sliderJoint );

		if ( visualRope )
		{
			MakeVisualRope( goJoint, pos1, toJoint, pos2 );
		}

		var propHelper2 = to.GetComponent<PropHelper>();
		if ( propHelper2.IsValid() )
		{
			propHelper2.OnComponentDestroy += () =>
			{
				goJoint.Destroy();
			};
		}

		return sliderJoint;
	}

	private static void MakeVisualRope( GameObject go1, Vector3 position1, GameObject go2, Vector3 position2, bool rigid = true, float slack = 0f, float radius = 1f )
	{
		VerletRope vertletRope = null;
		var splineInterpolation = 0;
		if ( !rigid )
		{
			vertletRope = go1.AddComponent<VerletRope>();
			vertletRope.Attachment = go2;

			const int maxSegmentCount = 48;
			// Maximum segment count, so long ropes don't exceed computation limits
			float length = position1.Distance( position2 ) + slack;
			int segmentCount = Math.Min( maxSegmentCount, MathX.CeilToInt( length / 16f ) );

			vertletRope.SegmentCount = segmentCount;
			vertletRope.Radius = radius;
			vertletRope.Slack = slack;
			splineInterpolation = 4;
		}

		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Width = radius;
		lineRenderer.Color = Color.White;
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.SplineInterpolation = splineInterpolation;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "materials/default/rope01.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;
		vertletRope?.LinkedRenderer = lineRenderer;
	}

	private static GameObject GetJointGameObject( GameObject go, int bone = -1 )
	{
		// Sandbox.Joint's only work with GameObjects, so we need to get the Bone's GameObject
		if ( bone > -1 && go.GetComponent<SkinnedModelRenderer>() is SkinnedModelRenderer skinnedModelRenderer )
		{
			var boneObj = skinnedModelRenderer.GetBoneObject( bone );
			if ( boneObj.IsValid() )
			{
				return boneObj;
			}
		}
		return go;
	}

	[Rpc.Broadcast]
	private void AddJointToList( Joint joint )
	{
		Joints.Add( joint );
	}

	[Rpc.Broadcast]
	public void RemoveConstraints( ConstraintType type, GameObject to = null )
	{
		foreach ( var j in Joints.AsEnumerable().Reverse() ) // reverse so we can remove items from the list while iterating
		{
			if ( !j.IsValid() )
			{
				Joints.Remove( j );
				continue;
			}

			if ( !to.IsValid() || JointMatchesGameObjects( j, to ) )
			{
				if ( j.GetConstraintType() == type )
				{
					j.Remove();
				}
			}
		}
	}
	private bool JointMatchesGameObjects( Joint joint, GameObject to )
	{
		var propHelper1 = joint?.GameObject?.GetComponentInParent<PropHelper>();
		var propHelper2 = joint?.Body?.GetComponentInParent<PropHelper>();
		return (propHelper1?.GameObject == this.GameObject && propHelper2?.GameObject == to) || (propHelper2?.GameObject == this.GameObject && propHelper1?.GameObject == to);
	}

	private void CheckCarAchievement( PropHelper propHelper2 )
	{
		if ( this.Joints.Count( x => x.GetConstraintType() == ConstraintType.Axis ) == 4 || (propHelper2.IsValid() && propHelper2.Joints.Count( x => x.GetConstraintType() == ConstraintType.Axis ) == 4) )
		{
			Analytics.UnlockAchievement( "myfirstcar" );
		}
	}
}

public static class JointExtensions
{
	public static void Remove( this Sandbox.Joint joint )
	{
		joint.GameObject?.GetComponentInParent<PropHelper>()?.Joints.Remove( joint );
		joint.Body?.GetComponentInParent<PropHelper>()?.Joints.Remove( joint );
		if ( joint.IsValid() )
		{
			if ( joint.GameObject?.Tags.Has( "jointHelper" ) ?? false ) joint.GameObject.Destroy();
			if ( joint.Body?.Tags.Has( "jointHelper" ) ?? false ) joint.Body.Destroy();
			joint.Destroy();
		}
		joint?.Destroy();
	}

	// ent.PhysicsGroup.Joints only appears to work for ModelDoc joints (eg. within a ragdoll), not for PhysicsJoint.Create'd ones, so lets track it ourselves
	public static List<Joint> GetJoints( this GameObject ent )
	{
		var jointTracker = ent.GetComponent<PropHelper>();
		if ( jointTracker is not null )
		{
			// Due to https://github.com/sboxgame/issues/issues/3949 OnBreak isn't called on Joint.Remove(), so we need to clean up here just in case
			jointTracker.Joints.RemoveAll( x => !x.IsValid() );
			return jointTracker.Joints;
		}
		return [];
	}

	public static IEnumerable<GameObject> GetAttachedGameObjects( this GameObject baseGo, HashSet<Joint> joints = null, HashSet<GameObject> objsChecked = null )
	{
		joints ??= new();
		objsChecked ??= new();
		Stack<GameObject> objsToCheck = new();
		if ( objsChecked.Add( baseGo ) )
			objsToCheck.Push( baseGo );

		while ( objsToCheck.Count > 0 )
		{
			GameObject ent = objsToCheck.Pop();
			foreach ( GameObject e in ent.Children )
			{
				if ( e.IsValid() && objsChecked.Add( e ) )
					objsToCheck.Push( e );
			}
			if ( ent.Parent.IsValid() && ent.Parent.GetComponent<Prop>() is not null && objsChecked.Add( ent.Parent ) )
			{
				objsToCheck.Push( ent.Parent );
			}

			// if ( ent.GetComponent<SkinnedModelRenderer>() is SkinnedModelRenderer ragdoll )
			// {
			// 	for ( int i = 0, end = ragdoll.Model.BoneCount; i < end; ++i )
			// 	{
			// 		GameObject e = ragdoll.GetBoneObject( i );
			// 		if ( objsChecked.Add( e ) )
			// 			objsToCheck.Push( e );
			// 	}
			// }
			foreach ( Joint j in GetJoints( ent ) )
			{
				if ( j.IsValid() && joints.Add( j ) )
				{
					if ( j.Body1.IsValid() && j.Body1.GetGameObject().IsValid() && objsChecked.Add( j.Body1.GetGameObject() ) )
						objsToCheck.Push( j.Body1.GetGameObject() );
					if ( j.Body2.IsValid() && j.Body2.GetGameObject().IsValid() && objsChecked.Add( j.Body2.GetGameObject() ) )
						objsToCheck.Push( j.Body2.GetGameObject() );
				}
			}
		}
		return objsChecked;
	}

	public static IEnumerable<T> GetAttachedGameObjects<T>( this GameObject baseGo, HashSet<Joint> joints = null )
	{
		return baseGo.GetAttachedGameObjects( joints ).Select( x => x.GetComponent<T>() ).Where( x => x is not null );
	}

	public static ConstraintType GetConstraintType( this Sandbox.Joint joint )
	{
		if ( joint is Sandbox.FixedJoint )
		{
			return ConstraintType.Weld;
		}
		else if ( joint is Sandbox.HingeJoint )
		{
			return ConstraintType.Axis;
		}
		else if ( joint is Sandbox.BallJoint )
		{
			return ConstraintType.BallSocket;
		}
		else if ( joint is Sandbox.SpringJoint springJoint )
		{
			if ( springJoint.MaxLength == 9999999 && !springJoint.EnableCollision )
			{
				return ConstraintType.Nocollide;
			}
			if ( springJoint.Frequency > 1000f )
			{
				return ConstraintType.Rope;
			}
			return ConstraintType.Spring;
		}
		else if ( joint is Sandbox.SliderJoint )
		{
			return ConstraintType.Slider;
		}
		throw new System.Exception( "Unknown joint type" );
	}

	public static bool IsWorld( this GameObject gameObject )
	{
		return gameObject.GetComponent<MapCollider>() != null;
	}
}

public enum ConstraintType
{
	Weld,
	Nocollide, // Generic
	Axis, // Revolute
	BallSocket, // Spherical
	Rope,
	Spring, // Winch/Hydraulic
	Slider, // Prismatic
}
