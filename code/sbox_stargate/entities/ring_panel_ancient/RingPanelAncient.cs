using Sandbox;

[Title( "Ring Panel (Ancient)" ), Category( "Stargate" ), Icon( "chair" ), Spawnable]
public partial class RingPanelAncient : RingPanel {

	protected override string[] ButtonsSounds { get; } = { "ringpanel.ancient.button1", "ringpanel.ancient.button2" };

	public override void Spawn()
	{
		base.Spawn();

		Transmit = TransmitType.Always;
		SetModel( "models/sbox_stargate/rings_panel/ancient/ring_panel_ancient.vmdl" );
		SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );

		PhysicsBody.BodyType = PhysicsBodyType.Static;

		CreateButtons();
	}

	public virtual void CreateButtons() // visible models of buttons that turn on/off and animate
	{
		for ( var i = 1; i <= 9; i++ )
		{
			var button = new RingPanelButton();
			button.SetModel( $"models/sbox_stargate/rings_panel/ancient/ring_panel_ancient_button_{i}.vmdl" );
			button.SetupPhysicsFromModel( PhysicsMotionType.Static, true ); // needs to have physics for traces
			button.PhysicsBody.BodyType = PhysicsBodyType.Static;
			button.EnableAllCollisions = false; // no collissions needed
			button.EnableTraceAndQueries = true; // needed for Use

			button.Position = Position;
			button.Rotation = Rotation;
			button.Scale = Scale;
			button.SetParent( this );

			var action = (i == 9) ? "DIAL" : i.ToString();
			button.Action = action;
			button.RingPanel = this;
			Buttons.Add( action, button );
		}
	}
}
