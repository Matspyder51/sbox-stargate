using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

[Title( "Brick Ramp" ), Category( "Stargate" ), Icon( "chair" ), Spawnable]
public partial class BrickRamp : Prop, IStargateRamp, IGateSpawner
{

	[Net]
	public Vector3 SpawnOffset { get; private set; } = new( 0, 0, 160 );
	public int AmountOfGates => 1;

	public Vector3[] StargatePositionOffset => new Vector3[] {
		new Vector3( 0, 0, -10 )
	};

	public Angles[] StargateRotationOffset => new Angles[] {
		Angles.Zero
	};

	public List<Stargate> Gate { get; set; } = new();

	public override void Spawn()
	{
		base.Spawn();
		Transmit = TransmitType.Default;

		SetModel( "models/sbox_stargate/ramps/brick/brick.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, true );

		Tags.Add( "solid" );
	}

	public void FromJson( JsonElement data )
	{
		Position = Vector3.Parse( data.GetProperty( "Position" ).ToString() );
		Rotation = Rotation.Parse( data.GetProperty( "Rotation" ).ToString() );

		PhysicsBody.BodyType = PhysicsBodyType.Static;
	}

	public object ToJson()
	{
		return new JsonModel()
		{
			EntityName = ClassName,
			Position = Position,
			Rotation = Rotation
		};
	}
}
