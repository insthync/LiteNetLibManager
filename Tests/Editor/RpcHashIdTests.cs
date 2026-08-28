using NUnit.Framework;
using System.Collections.Generic;

namespace LiteNetLibManager.Tests
{
    public class RpcHashIdTests
    {
        [Test]
        public void GetHashedId_IsDeterministic()
        {
            string id = "MyGame.Behaviours.PlayerBehaviour_0_UpdateHealth";
            Assert.AreEqual(LiteNetLibIdentity.GetHashedId(id), LiteNetLibIdentity.GetHashedId(id));
        }

        [Test]
        public void GetHashedId_IsOrderSensitive()
        {
            Assert.AreNotEqual(LiteNetLibIdentity.GetHashedId("AB"), LiteNetLibIdentity.GetHashedId("BA"));
        }

        [Test]
        public void GetHashedId_IgnoresCharactersAfterNullTerminator()
        {
            // The hash loop stops at '\0', so these ids hash identically by design
            Assert.AreEqual(LiteNetLibIdentity.GetHashedId("A"), LiteNetLibIdentity.GetHashedId("A\0B"));
        }

        [Test]
        public void GetHashedId_EmptyString_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LiteNetLibIdentity.GetHashedId(string.Empty));
        }

        [Test]
        public void GetHashedId_DistinctMethodIds_HaveNoCollisions()
        {
            // Simulates the id space of a large project: FullName_behaviourIndex_methodName.
            // The id set is fixed (not random), so this is deterministic - if it passes once
            // it passes always, and if it fails a real hash collision exists in this id set.
            HashSet<string> idSet = new HashSet<string>();
            HashSet<int> hashSet = new HashSet<int>();
            for (int i = 0; i < 100; ++i)
            {
                for (int j = 0; j < 10; ++j)
                {
                    string id = $"MyGame.Module{i}.Behaviour{i}_{j}_DoAction";
                    idSet.Add(id);
                    hashSet.Add(LiteNetLibIdentity.GetHashedId(id));
                }
            }
            Assert.AreEqual(1000, idSet.Count);
            Assert.AreEqual(idSet.Count, hashSet.Count,
                "Hash collision detected within the tested id set - registration order would silently change which rpc gets called.");
        }

        [Test]
        public void GetHashedId_KnownIds_AreStableAcrossVersions()
        {
            // Golden values: if one of these changes, wire compatibility with
            // already-deployed builds is broken. Update them only deliberately.
            Assert.AreEqual(1131510032, LiteNetLibIdentity.GetHashedId("TestBehaviour_0_DoAction"));
            Assert.AreEqual(78271862, LiteNetLibIdentity.GetHashedId("Player_0_UpdateHealth"));
        }
    }
}
