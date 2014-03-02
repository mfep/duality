﻿using Duality;
using OpenTK;
using System;
using System.Collections.Generic;

namespace Duality.Plugins.Steering
{
	/// <summary>
	/// Creates velocity samples which are going to get tested with <see cref="ICharacteristics"/>. 
	/// If the samples are poorly chosen or if there are simply not enough samples the agent won't be able to
	/// choose "good" velocities which lead to a bad steering quality. If on the other hand to many samples are
	/// generated the performance will suffer because for every sample the agent needs to calculate time of imapacts
	/// with obstacles.
	/// </summary>
	public interface IVelocitySampler
	{
		/// <summary>
		/// This method is called in every time step for every agent before the sampling starts.
		/// If your implementation is adaptive you should throw away your old state here and start over.
		/// </summary>
		void Reset();
		/// <summary>
		/// Get the current sample velocity. The implementation is free to use internal information gathered from
		/// previous calls to <see cref="IVelocitySampler.SetCurrentCost"/>. You should make sure that your implementation
		/// samples the zero-velocity.
		/// </summary>
		/// <returns>Velocity which should be evaluated</returns>
		Vector2 GetCurrentSample(Agent agent);
		/// <summary>
		/// Feeds the evaluated cost back into the sampler. The cost value can be used to adapt and intelligent choose the next
		/// velocities.
		/// </summary>
		/// <param name="cost">The cost which was returned from <see cref="ICharacteristics.CalculateVelocityCost"/> 
		/// with the current velocity as parameter
		/// </param>
		/// <returns>
		/// <code>true</code> if more velocities should be sampled and <code>false</code> if 
		/// no new velocities should be sampled.
		/// </returns>
		bool SetCurrentCost(float cost);
	}

	/// <summary>
	/// Simple brute force implementation of <see cref="IVelocitySampler"/>. Velocities are equally distributed in all directions
	/// independent of the costs which are fed back.
	/// </summary>
	[Serializable]
	public class BruteForceVelocitySampler : IVelocitySampler
	{
		int layerCount;
		int outerLayerSampleCount;

		[NonSerialized]
		private int currentSampleIdx = 0;

		public BruteForceVelocitySampler(int layerCount, int outerLayerSampleCount)
		{
			this.layerCount = layerCount;
			this.outerLayerSampleCount = outerLayerSampleCount;
		}

		public void Reset()
		{
			currentSampleIdx = 0;
		}

		public Vector2 GetCurrentSample(Agent agent)
		{
			if (currentSampleIdx >= layerCount * outerLayerSampleCount)
				return Vector2.Zero;

			var layerIdx = currentSampleIdx % layerCount;
			var directionIdx = (currentSampleIdx / layerCount) % outerLayerSampleCount;

			float angle = ((float)directionIdx / outerLayerSampleCount) * MathF.RadAngle360;
			float speedFactor = (float)(layerIdx + 1) / layerCount;
			return new Vector2(MathF.Cos(angle) * speedFactor, MathF.Sin(angle) * speedFactor);
		}

		public bool SetCurrentCost(float cost)
		{
			currentSampleIdx++;
			if (currentSampleIdx <= layerCount * outerLayerSampleCount)
				return true;
			else
				return false;
		}
	}
	

	/// <summary>
	/// Samples velocities based on the velocity the agent chose. The sampling
	/// density is higher velocities close to the last best velocity.
	/// This reduces samples needed massively compared to <see cref="BruteForceVelocitySampler"/>
	/// but can potentially lead to undesired behavior.
	/// </summary>
	[Serializable]
	public class AdaptiveVelocitySampler : IVelocitySampler
	{
		int layerCount;
		int outerLayerSampleCount;

		[NonSerialized]
		private int currentSampleIdx = 0;

		public AdaptiveVelocitySampler(int layerCount, int outerLayerSampleCount)
		{
			this.layerCount = layerCount;
			this.outerLayerSampleCount = outerLayerSampleCount;
		}

		public void Reset()
		{
			currentSampleIdx = 0;
		}

		public Vector2 GetCurrentSample(Agent agent)
		{
			var commonSampleCount = layerCount * outerLayerSampleCount;
			var oldVelocity = agent.BestVel / agent.Characteristics.MaxSpeed;
			
			if(currentSampleIdx >= commonSampleCount + 1)
				return Vector2.Zero;
			if (currentSampleIdx >= commonSampleCount)
				return oldVelocity;

			var layerIdx = currentSampleIdx % layerCount;
			var directionIdx = (currentSampleIdx / layerCount) % outerLayerSampleCount;


			float undistortedSpeedFactor = (float)(layerIdx + 1) / layerCount;
			float undistortedAngle = MathF.Lerp(-1f, 1f, ((float)directionIdx / outerLayerSampleCount));

			float speedFactor = undistortedSpeedFactor;
			float angle = MathF.Atan2(oldVelocity.Y, oldVelocity.X) + MathF.Pow(Math.Abs(undistortedAngle), 0.8f) * undistortedAngle * MathF.RadAngle180;
				
			return new Vector2(MathF.Cos(angle) * speedFactor, MathF.Sin(angle) * speedFactor);
		}

		public bool SetCurrentCost(float cost)
		{
			currentSampleIdx++;
			if (currentSampleIdx <= layerCount * outerLayerSampleCount)
				return true;
			else
				return false;
		}
	}

	public class CommonVelocitySampler {
		private CommonVelocitySampler()
		{
		}

		public static IVelocitySampler QUALITY = new BruteForceVelocitySampler(3, 128);
		public static IVelocitySampler PERFORMANCE = new AdaptiveVelocitySampler(5, 11);
	}
}