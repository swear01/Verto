using NUnit.Framework;
using UnityEngine;

// Every frame advances exactly 1/60 s of game time and is not paced against the wall clock,
// so WaitForSeconds and the controller's accumulator cost frames instead of real seconds.
[SetUpFixture]
public class FastTime
{
    float capture;
    [OneTimeSetUp] public void Start() { capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60; }
    [OneTimeTearDown] public void Stop() { Time.captureDeltaTime = capture; }
}
