﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

public partial class StargateRingPegasus : ModelEntity
{
	// ring variables

	[Net]
	public StargatePegasus Gate { get; set; } = null;

	public string RingSymbols { get; private set; } = "?0JKNTR3MBZX*H69IGPL#@QFS1E4AU85OCW72YVD";

	public List<ModelEntity> SymbolParts { get; private set; } = new();

	public List<int> DialSequenceActiveSymbols { get; private set; } = new();

	private Sound? RollSound = null;


	public override void Spawn()
	{
		base.Spawn();
		Transmit = TransmitType.Always;

		SetModel( "models/sbox_stargate/gate_atlantis/ring_atlantis.vmdl" );
		EnableAllCollisions = false;

		CreateSymbolParts();
	}

	// create symbols
	// symbol models

	public void AddSymbolPart(string name)
	{
		var part = new ModelEntity( name );
		part.Position = Position;
		part.Rotation = Rotation;
		part.SetParent( this );
		part.Transmit = TransmitType.Always;
		part.EnableAllCollisions = false;

		SymbolParts.Add( part );
	}

	public void CreateSymbolParts()
	{
		AddSymbolPart( "models/sbox_stargate/gate_atlantis/ring_atlantis_symbols_1_18.vmdl" );
		AddSymbolPart( "models/sbox_stargate/gate_atlantis/ring_atlantis_symbols_19_36.vmdl" );
	}

	protected override void OnDestroy()
	{
		foreach (var part in SymbolParts)
		{
			if ( Game.IsServer && part.IsValid() ) part.Delete();
		}

		base.OnDestroy();
	}

	public int GetSymbolNum(int num)
	{
		return num.UnsignedMod( 36 );
	}

	public int GetSymbolNumFromChevron(int chevNum)
	{
		return GetSymbolNum((4 * chevNum) - 1);
	}

	public async void SetSymbolState(int num, bool state, float delay = 0)
	{
		if (delay > 0)
		{
			await Task.DelaySeconds( delay );
			if ( this.IsValid() ) return;
		}

		num = num.UnsignedMod( 36 );
		var isPart1 = num < 18;
		SymbolParts[isPart1 ? 0 : 1].SetBodyGroup( (isPart1 ? num : num - 18), state ? 1 : 0 );
	}

	public void RollSymbol(int start, int count, bool counterclockwise = false, float time = 2.0f)
	{
		if ( start < 0 || start > 35 ) return;

		var startTime = Time.Now;
		var delay = time / (count + 1);

		try
		{
			for ( var i = 0; i <= count; i++ )
			{
				var i_copy = i;
				var taskTime = startTime + (delay * i_copy);

				void rollSym()
				{
					if ( Gate.ShouldStopDialing ) return;

					var symIndex = counterclockwise ? (start - i_copy) : start + i_copy;
					var symPrevIndex = counterclockwise ? (symIndex + 1) : symIndex - 1;

					SetSymbolState( symIndex, true );
					if ( !DialSequenceActiveSymbols.Contains( symPrevIndex.UnsignedMod( 36 ) ) ) SetSymbolState( symPrevIndex, false );

					if ( i_copy == count )
					{
						DialSequenceActiveSymbols.Add( symIndex.UnsignedMod( 36 ) );
					}
				}

				Gate.AddTask( taskTime, rollSym, Stargate.TimedTaskCategory.DIALING );
			}
		}
		catch (Exception) { }
	}

	public void ResetSymbols(bool clearDialActive = true)
	{
		for ( int i = 0; i <= 35; i++ )	SetSymbolState( i, false );
		if ( clearDialActive ) DialSequenceActiveSymbols.Clear();
	}

	public void ResetSymbol( int num, bool clearDialActive = true )
	{
		Log.Info( $"Reset symbol with index {num}" );
		SetSymbolState( num, false );
		if ( clearDialActive ) DialSequenceActiveSymbols.Remove( num );
	}

	public void LightupSymbols()
	{
		for ( int i = 0; i <= 35; i++ ) SetSymbolState( i, true );
	}

	public void PlayRollSound(bool fast = false)
	{
		StopRollSound();
		RollSound = PlaySound( Gate.GetSound( fast ? "gate_roll_fast" : "gate_roll_slow" ) );
	}

	public void StopRollSound()
	{
		if ( RollSound.HasValue ) RollSound.Value.Stop();
	}

	// INBOUND
	public void RollSymbolsInbound( float time, float startDelay = 0, int chevCount = 7 )
	{
		try
		{
			var startTime = Time.Now;

			void firstRun()
			{
				ResetSymbols();
				SetSymbolState( 0, true );
			}

			var pegasusSymbolChevrons = new Dictionary<int, int>() { { 3, 1 }, { 7, 2 }, { 11, 3 }, { 15, 8 }, { 19, 9 }, { 23, 4 }, { 27, 5 }, { 31, 6 }, { 35, 7 } };

			var delay = time / 35f;
			for ( int i = 0; i <= 35; i++ )
			{
				var i_copy = i;
				var taskTime = startTime + startDelay + (delay * i_copy);

				Gate.AddTask( taskTime, i_copy == 0 ? firstRun : () => SetSymbolState( i_copy, true ), Stargate.TimedTaskCategory.DIALING );

				if ( (i + 1) % 4 == 0 )
				{
					var chev = Gate.GetChevron( pegasusSymbolChevrons[i] );
					void chevAction() => Gate.ChevronActivate( chev, delay * 0.5f, true, i_copy == 35, i_copy == 11 && chevCount == 7, i_copy == 31 );

					if ( (chevCount == 7 && i != 15 && i != 19) || (chevCount == 8 && i != 19) || (chevCount == 9) )
					{
						Gate.AddTask( taskTime, chevAction, Stargate.TimedTaskCategory.DIALING );
					}
				}
			}

		}
		catch ( Exception ) { }
	}

	public void DoSymbolsInboundInstant()
	{
		for ( int i = 0; i <= 35; i++ ) SetSymbolState( i, true );
	}

	// SLOWDIAL
	public void RollSymbolsDialSlow( int chevCount, Func<bool> validCheck )
	{
		try
		{
			ResetSymbols();

			var dataSymbols7 = new int[7, 2] { { 35, 32 }, { 3, 40 }, { 7, 32 }, { 11, 48 }, { 23, 32 }, { 27, 40 }, { 31, 32 } };
			var dataSymbols8 = new int[8, 2] { { 35, 32 }, { 3, 40 }, { 7, 32 }, { 11, 48 }, { 23, 32 }, { 27, 40 }, { 31, 52 }, { 15, 56 } };
			var dataSymbols9 = new int[9, 2] { { 35, 32 }, { 3, 40 }, { 7, 32 }, { 11, 48 }, { 23, 32 }, { 27, 40 }, { 31, 52 }, { 15, 40 }, { 19, 56 } };

			var data = (chevCount == 9) ? dataSymbols9 : ((chevCount == 8) ? dataSymbols8 : dataSymbols7);

			var rollStartDelay = 0.75f;
			var delayBetweenSymbols = 1.25f;
			var startTime = Time.Now;

			var elapsedTime = 0f;
			for ( int i = 0; i < chevCount; i++ )
			{
				var i_copy = i;
				var startPos = data[i_copy, 0];
				var symSteps = data[i_copy, 1];
				var symRollTime = symSteps * 0.05f;

				var rollSoundTaskTime = startTime + elapsedTime;
				var symTaskTime = startTime + elapsedTime + rollStartDelay;
				var chevTaskTime = startTime + elapsedTime + rollStartDelay + symRollTime;

				elapsedTime += rollStartDelay + symRollTime + delayBetweenSymbols;

				Gate.AddTask( rollSoundTaskTime, () => PlayRollSound(), Stargate.TimedTaskCategory.DIALING );
				Gate.AddTask( symTaskTime, () => RollSymbol( startPos, symSteps, i_copy % 2 == 0, symRollTime ), Stargate.TimedTaskCategory.DIALING );

				void chevTask()
				{
					StopRollSound();

					var chev = Gate.GetChevronBasedOnAddressLength( i_copy + 1, chevCount );
					if ( i_copy < chevCount - 1 ) Gate.ChevronActivateDHD( chev, 0, true );
					else Gate.ChevronActivate( chev, 0, validCheck(), true );
				}
				Gate.AddTask( chevTaskTime, chevTask, Stargate.TimedTaskCategory.DIALING );
			}
		}
		catch ( Exception ) { }
	}

	// FASTDIAL
	public void RollSymbolsDialFast( int chevCount, Func<bool> validCheck )
	{
		try
		{
			ResetSymbols();

			PlayRollSound( true );

			var symRollTime = 5f / chevCount;
			var delayBetweenSymbols = 1.5f / (chevCount - 1);

			var dataSymbols7 = new List<int>() { 27, 19, 35, 35, 15, 7, 23 };
			var dataSymbols8 = new List<int>() { 27, 19, 35, 35, 15, 7, 3, 11 };
			var dataSymbols9 = new List<int>() { 27, 19, 35, 35, 15, 7, 3, 31, 23 };

			var data = (chevCount == 9) ? dataSymbols9 : ((chevCount == 8) ? dataSymbols8 : dataSymbols7);

			var startTime = Time.Now;
			for ( int i = 0; i < chevCount; i++ )
			{
				var i_copy = i;

				var symTaskTime = startTime + (symRollTime + delayBetweenSymbols) * (i_copy);
				Gate.AddTask( symTaskTime, () => RollSymbol( data[i_copy], 12, i_copy % 2 == 1, symRollTime ), Stargate.TimedTaskCategory.DIALING );

				var chevTaskTime = startTime + (symRollTime + delayBetweenSymbols) * (i_copy + 1) - delayBetweenSymbols;
				Gate.AddTask( chevTaskTime, () => Gate.ChevronActivate( Gate.GetChevronBasedOnAddressLength( i_copy + 1, chevCount ), 0, i_copy == chevCount - 1 ? validCheck() : true, i_copy == chevCount - 1 ), Stargate.TimedTaskCategory.DIALING );
			}
		}
		catch ( Exception ) { }
	}

	public void RollSymbolDHDFast( int chevCount, Func<bool> validCheck, int chevNum, float symRollTime )
	{
		try
		{
			var dataSymbols7 = new List<int>() { 27, 19, 35, 35, 15, 7, 23 };
			var dataSymbols8 = new List<int>() { 27, 19, 35, 35, 15, 7, 3, 11 };
			var dataSymbols9 = new List<int>() { 27, 19, 35, 35, 15, 7, 3, 31, 23 };

			var data = (chevCount == 9) ? dataSymbols9 : ((chevCount == 8) ? dataSymbols8 : dataSymbols7);

			var isLast = chevNum == chevCount;

			var startTime = Time.Now;
			var symTaskTime = startTime;
			Gate.AddTask( symTaskTime, () => RollSymbol( data[chevNum - 1], 12, (chevNum - 1) % 2 == 1, symRollTime ), Stargate.TimedTaskCategory.SYMBOL_ROLL_PEGASUS_DHD );

			var chevTaskTime = startTime + symRollTime;
			Gate.AddTask( chevTaskTime, () => Gate.ChevronActivateDHD( Gate.GetChevronBasedOnAddressLength( chevNum, chevCount ), 0, isLast ? validCheck() : true ), Stargate.TimedTaskCategory.SYMBOL_ROLL_PEGASUS_DHD );
		}
		catch ( Exception ) { }
	}

	// DEBUG
	public void DrawSymbols()
	{
		if ( !this.IsValid() ) return;

		var deg = 10;
		var ang = Rotation.Angles();
		for ( int i = 0; i < 36; i++ )
		{
			var rotAng = ang.WithRoll( ang.roll - (i * deg) - deg);
			var newRot = rotAng.ToRotation();
			var pos = Position + newRot.Forward * 4 + newRot.Up * 117.5f;
			DebugOverlay.Text( i.ToString(), pos, Color.Yellow );
		}
	}

	[Event.Client.Frame]
	public void RingSymbolsDebug()
	{
		//DrawSymbols();
	}

}
