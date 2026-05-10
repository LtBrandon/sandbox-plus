using Sandbox.Rendering;
using Sandbox.Utility;

public partial class PhysGun
{
	[Property] public LineRenderer BeamRenderer { get; set; }
	[Property] public GameObject EndPointEffectPrefab { get; set; }
	[Property] public GameObject FreezeEffectPrefab { get; set; }
	[Property] public GameObject UnFreezeEffectPrefab { get; set; }
	[Property] public GameObject GrabEffectPrefab { get; set; }
	
	GameObject _endPointEffect;
	GameObject _grabEffect;

	GameObject lastGrabbedObject;
	
	Vector3.SpringDamped middleSpring = new Vector3.SpringDamped( 0, 0 );
	private float _prevBeamDistance = 0;

	[Rpc.Broadcast]
	protected virtual void KillEffects()
	{
		if ( _endPointEffect.IsValid() )
		{
			_endPointEffect?.Destroy();
			_endPointEffect = null;
		}

		if ( _grabEffect.IsValid() )
		{
			_grabEffect?.Destroy();
			_grabEffect = null;
		}

		if (BeamRenderer.IsValid())
		{
			BeamRenderer.GameObject.Enabled = false;
		}

		DisableHighlights( lastGrabbedObject );
		lastGrabbedObject = null;
	}

	private void DisableHighlights( GameObject gameObject )
	{
		if ( gameObject.IsValid() )
		{
			foreach ( var child in gameObject.Children )
			{
				if ( !child.Components.Get<ModelRenderer>().IsValid() )
					continue;

				if ( child.Components.TryGet<HighlightOutline>( out var childglow ) )
				{
					childglow.Destroy();
				}
			}

			if ( gameObject.Components.TryGet<HighlightOutline>( out var glow ) )
			{
				glow.Destroy();
			}
		}
	}

	Vector3 lastBeamPos;

	protected virtual void UpdateEffects()
	{
		if ( !Owner.IsValid() || !Beaming )
		{
			KillEffects();
			return;
		}

		if ( grabbed && !GrabbedObject.IsValid() )
		{
			DisableHighlights( lastGrabbedObject );
		}

		var startPos = Owner.EyeTransform.Position;
		var endPos = startPos + Owner.EyeTransform.Forward * MaxTargetDistance;
		var dir = Owner.EyeTransform.Forward;

		var tr = Scene.Trace.Ray( startPos, endPos )
			.UseHitboxes()
			.IgnoreGameObject( Owner.GameObject )
			.WithAllTags( "solid" )
			.WithoutTags( "player" )
			.Run();

		var muzzleAttachment = Attachment("muzzle");
		var rotation = muzzleAttachment.Rotation;
		startPos = muzzleAttachment.Position + dir * 10f;
		endPos = tr.EndPosition;

		if ( GrabbedObject.IsValid() && !GrabbedObject.Tags.Contains( "world" ) && HeldBody.IsValid() )
		{
			var physGroup = HeldBody.PhysicsGroup;

			if ( physGroup != null && GrabbedBone >= 0 )
			{
				var physBody = physGroup.GetBody( GrabbedBone );
				if ( physBody != null )
				{
					endPos = physBody.Transform.PointToWorld(GrabbedPos);
				}
			}
			else
			{
				if ( !HeldBody.IsValid() )
					return;

				endPos = HeldBody.Transform.PointToWorld(GrabbedPos);
			}

			if ( GrabbedObject.GetComponent<ModelRenderer>().IsValid() )
			{
				lastGrabbedObject = GrabbedObject;

				var glow = GrabbedObject.GetOrAddComponent<HighlightOutline>();
				glow.Width = 0.25f;
				glow.Color = new Color( 4f, 50.0f, 70.0f, 1.0f );
				glow.ObscuredColor = new Color( 4f, 50.0f, 70.0f, 0.0005f );

				foreach ( var child in lastGrabbedObject.Children )
				{
					if ( !child.GetComponent<ModelRenderer>().IsValid() )
						continue;

					glow = child.GetOrAddComponent<HighlightOutline>();
					glow.Color = new Color( 0.1f, 1.0f, 1.0f, 1.0f );
				}
			}
		}
		else
		{
			endPos = tr.EndPosition;
		}
		
		var endTx = new Transform(endPos, rotation);
		
		if ( grabbed )
		{
			if ( _endPointEffect != null )
			{
				ITemporaryEffect.DisableLoopingEffects( _endPointEffect );
				_endPointEffect = null;
			}


			if ( !_grabEffect.IsValid() )
			{
				_grabEffect = GrabEffectPrefab.Clone( endTx );
			}

			if ( _grabEffect.IsValid() )
			{
				_grabEffect.WorldTransform = endTx;
			}

		}
		else
		{
			if ( _grabEffect != null )
			{
				_grabEffect.Destroy();
				_grabEffect = null;
			}

			if ( !_endPointEffect.IsValid() )
			{
				_endPointEffect = EndPointEffectPrefab.Clone( endTx );
			}

			if ( _endPointEffect.IsValid() )
			{
				_endPointEffect.WorldTransform = endTx;
			}
		}
		
		bool justEnabled = !BeamRenderer.GameObject.Enabled;
		
		if ( BeamRenderer.VectorPoints == null || BeamRenderer.VectorPoints.Count != 4 )
			BeamRenderer.VectorPoints = new List<Vector3>( [0, 0, 0, 0] );
		
		var distance = startPos.Distance( endPos );
		var targetMiddle = startPos + rotation.Forward * distance * 0.33f;
		targetMiddle += Noise.FbmVector(2, Time.Now * 400f, Time.Now * 100f);
		
		if ( !justEnabled )
		{
			// If the beam halved or more in a single frame, snap the spring to the new position to avoid shakiness
			if ( _prevBeamDistance > 1f && distance / _prevBeamDistance < 0.5f )
			{
				middleSpring = new Vector3.SpringDamped( targetMiddle, targetMiddle, 4, 0.2f );
			}

			// Ensure the middle point is never behind the first one
			var alongFwd = Vector3.Dot( middleSpring.Current - startPos, rotation.Forward );
			if ( alongFwd < 0 )
			{
				var clamped = middleSpring.Current - rotation.Forward * alongFwd;
				middleSpring = new Vector3.SpringDamped( clamped, targetMiddle, 4, 0.2f );
			}
		}

		lastBeamPos = endPos;
		BeamRenderer.VectorPoints[0] = startPos;
		BeamRenderer.VectorPoints[1] = middleSpring.Current;
		middleSpring.Target = targetMiddle;
		middleSpring.Update( Time.Delta );
		BeamRenderer.VectorPoints[2] = Vector3.Lerp(endPos + rotation.Backward * 10, BeamRenderer.VectorPoints[1], 0.3f + MathF.Sin(Time.Now * 10f) * 0.2f);
		BeamRenderer.VectorPoints[3] = endPos;
		
		if ( justEnabled )
		{
			BeamRenderer.GameObject.Enabled = true;
			_prevBeamDistance = distance;
			BeamRenderer.VectorPoints[1] = targetMiddle;
			middleSpring = new Vector3.SpringDamped( targetMiddle, targetMiddle, 4, 0.2f );
		}

	}

	private void FreezeEffects()
	{
		var effect = FreezeEffectPrefab.Clone( HeldBody.GameObject.WorldTransform );
		foreach ( var emitter in effect.GetComponentsInChildren<ParticleModelEmitter>() )
		{
			emitter.Target = HeldBody.GameObject;
		}
	}

	private void UnFreezeEffects()
	{
		var effect = UnFreezeEffectPrefab.Clone( HeldBody.GameObject.WorldTransform );
		foreach ( var emitter in effect.GetComponentsInChildren<ParticleModelEmitter>() )
		{
			emitter.Target = HeldBody.GameObject;
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		KillEffects();
	}

	void INetworkListener.OnDisconnected( Connection channel )
	{
		if ( channel == Owner.Network.Owner && this.IsValid() )
			KillEffects();
	}
}
