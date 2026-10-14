// SPDX-License-Identifier: MIT

#nullable enable

using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Parallax.Data;
using Content.Client.Parallax.Managers;
using Robust.Shared.Maths;

namespace Content.IntegrationTests
{
    public sealed class DummyParallaxManager : IParallaxManager
    {
        public Vector2 ParallaxAnchor { get; set; }

        public bool IsLoaded(string name)
        {
            return true;
        }

        public ParallaxLayerPrepared[] GetParallaxLayers(string name)
        {
            return Array.Empty<ParallaxLayerPrepared>();
        }

        public void LoadDefaultParallax()
        {
        }

        public Task LoadParallaxByName(string name)
        {
            return Task.CompletedTask;
        }

        public void UnloadParallax(string name)
        {
        }

        public bool StartTransition(string current, string target, float duration = 8f, float delay = 0f, string? hyper = null)
        {
            return false;
        }

        public bool RequestTransition(string current, string target, float duration, float delay, string? hyper = null)
        {
            return false;
        }

        public string? GetTransitionHyper()
        {
            return null;
        }

        public float GetTransitionProgress()
        {
            return -1f;
        }

        public string? GetTransitionFrom()
        {
            return null;
        }

        public string? GetTransitionTo()
        {
            return null;
        }

        public string? GetCurrent()
        {
            return null;
        }

        public string? GetRequested()
        {
            return null;
        }

        public (float scrollClock, float zoomAmount) SampleTravel()
        {
            return (0f, 1f);
        }

        public void AdvanceFrame()
        {
        }

        public void ResetRoundState()
        {
        }

        public Vector2 AdvanceScreenScroll(ParallaxLayerConfig config, Vector2 scrolling, Angle rotation)
        {
            return Vector2.Zero;
        }

        public Vector2 AdvanceDrift(ParallaxLayerConfig config, Vector2 drift, Angle rotation)
        {
            return Vector2.Zero;
        }

        public void StartWarp(float duration = 8.5f, float peakSpeed = 3f, float peakZoom = 1.25f)
        {
        }

        public bool IsWarping => false;

        public float WarpSpeedFraction => 0f;

        public float WarpProgress => 0f;
    }
}
