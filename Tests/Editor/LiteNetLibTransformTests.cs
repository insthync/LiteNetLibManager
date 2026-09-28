using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LiteNetLibManager.Tests
{
    public class LiteNetLibTransformTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SingleReceivedSample_ReachesItsFinalPose()
        {
            var gameObject = new GameObject("single transform sample");
            try
            {
                var networkTransform = gameObject.AddComponent<LiteNetLibTransform>();
                networkTransform.interpolationTicks = 2;
                var buffers = (SortedList<uint, LiteNetLibTransform.TransformData>)
                    typeof(LiteNetLibTransform).GetField("_interpBuffers", PrivateInstance).GetValue(networkTransform);
                buffers.Add(10, new LiteNetLibTransform.TransformData
                {
                    Tick = 10,
                    SyncData = LiteNetLibTransform.SyncTransformState.PositionX,
                    Position = new Vector3(7f, 0f, 0f),
                });
                typeof(LiteNetLibTransform).GetField("_interpTick", PrivateInstance).SetValue(networkTransform, 11u);

                var interpolate = typeof(LiteNetLibTransform).GetMethod("InterpolateTransform", PrivateInstance);
                interpolate.Invoke(networkTransform, null);
                Assert.AreEqual(0f, gameObject.transform.position.x, "The sample should wait for the render tick");

                typeof(LiteNetLibTransform).GetField("_interpTick", PrivateInstance).SetValue(networkTransform, 12u);
                interpolate.Invoke(networkTransform, null);
                Assert.AreEqual(7f, gameObject.transform.position.x);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
