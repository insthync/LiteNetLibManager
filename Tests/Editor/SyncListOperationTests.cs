using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLibManager.Tests
{
    public class SyncListOperationTests
    {
        /// <summary>
        /// Exposes the operation log so tests can exercise the coalescing engine
        /// without a spawned identity (recording operations through the public API
        /// alone requires IsSpawned - PlayMode territory). Each *Op helper mirrors
        /// exactly what the spawned path does: mutate the local list via the public
        /// API, then append the operation entry the spawned list would have logged.
        /// WriteSyncData/ReadSyncData have no spawn checks, so wire round trips are
        /// fully exercised.
        /// </summary>
        private class TestIntSyncList : LiteNetLibSyncList<int>
        {
            public void AddOp(int item)
            {
                Add(item);
                PrepareOperation(_operationEntries, LiteNetLibSyncListOp.Add, Count - 1, item);
            }

            public void SetOp(int index, int item)
            {
                Set(index, item);
                PrepareOperation(_operationEntries, LiteNetLibSyncListOp.Set, index, item);
            }

            public void DirtyOp(int index)
            {
                Dirty(index);
                PrepareOperation(_operationEntries, LiteNetLibSyncListOp.Dirty, index, this[index]);
            }

            public void InsertOp(int index, int item)
            {
                Insert(index, item);
                PrepareOperation(_operationEntries, LiteNetLibSyncListOp.Insert, index, item);
            }

            public void RemoveAtOp(int index)
            {
                int item = this[index];
                RemoveAt(index);
                PrepareOperation(_operationEntries, LiteNetLibSyncListOp.RemoveAt, index, item);
            }

            public void ClearOp()
            {
                Clear();
                PrepareOperation(_operationEntries, LiteNetLibSyncListOp.Clear, -1, default);
            }

            public int[] Snapshot()
            {
                return _list.ToArray();
            }
        }

        private TestIntSyncList _list;

        [SetUp]
        public void SetUp()
        {
            _list = new TestIntSyncList();
        }

        private int ReadOperationCount(bool initial)
        {
            var writer = new NetDataWriter();
            _list.WriteSyncData(0, initial, writer);
            var reader = new NetDataReader(writer.CopyData());
            return reader.GetPackedInt();
        }

        private TestIntSyncList WriteAndApplyDeltaTo(TestIntSyncList target)
        {
            var writer = new NetDataWriter();
            _list.WriteSyncData(0, false, writer);
            var reader = new NetDataReader(writer.CopyData());
            target.ReadSyncData(0, false, reader);
            return target;
        }

        [Test]
        public void Set_AtSameIndex_CoalescesPriorSet()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.SetOp(0, 9);
            _list.SetOp(0, 7);
            Assert.AreEqual(3, ReadOperationCount(false));

            TestIntSyncList target = WriteAndApplyDeltaTo(new TestIntSyncList());
            CollectionAssert.AreEqual(new[] { 7, 2 }, target.Snapshot());
        }

        [Test]
        public void Dirty_AtSameIndex_CoalescesPriorDirty()
        {
            _list.AddOp(1);
            _list.DirtyOp(0);
            _list.DirtyOp(0);
            Assert.AreEqual(2, ReadOperationCount(false));

            TestIntSyncList target = WriteAndApplyDeltaTo(new TestIntSyncList());
            CollectionAssert.AreEqual(new[] { 1 }, target.Snapshot());
        }

        [Test]
        public void Set_ReplacesPriorDirtyAtSameIndex()
        {
            _list.AddOp(1);
            _list.DirtyOp(0);
            _list.SetOp(0, 5);
            Assert.AreEqual(2, ReadOperationCount(false));

            TestIntSyncList target = WriteAndApplyDeltaTo(new TestIntSyncList());
            CollectionAssert.AreEqual(new[] { 5 }, target.Snapshot());
        }

        [Test]
        public void RemoveAt_KeepsSetAtOtherIndexes()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.SetOp(1, 9);
            _list.RemoveAtOp(0);
            Assert.AreEqual(4, ReadOperationCount(false));

            TestIntSyncList target = WriteAndApplyDeltaTo(new TestIntSyncList());
            CollectionAssert.AreEqual(new[] { 9 }, target.Snapshot());
        }

        [Test]
        public void Clear_ReplacesAllPriorOperations()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.ClearOp();
            Assert.AreEqual(1, ReadOperationCount(false));

            TestIntSyncList target = WriteAndApplyDeltaTo(new TestIntSyncList());
            CollectionAssert.AreEqual(new int[0], target.Snapshot());
        }

        [Test]
        public void Synced_ClearsPendingOperationLog()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.Synced(0, false);
            Assert.AreEqual(0, ReadOperationCount(false));
        }

        [Test]
        public void InitialSync_RoundTripsFullState()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.AddOp(3);

            var writer = new NetDataWriter();
            _list.WriteSyncData(0, true, writer);
            var reader = new NetDataReader(writer.CopyData());

            TestIntSyncList target = new TestIntSyncList();
            target.ReadSyncData(0, true, reader);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, target.Snapshot());
        }

        [Test]
        public void DeltaSync_RoundTripsOperations()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.AddOp(3);
            _list.Synced(0, false);

            _list.AddOp(4);
            _list.SetOp(1, 20);
            _list.RemoveAtOp(0);

            Assert.AreEqual(3, ReadOperationCount(false));

            // Deltas apply on top of the state the client already has (baseline [1, 2, 3])
            TestIntSyncList target = new TestIntSyncList();
            target.AddOp(1);
            target.AddOp(2);
            target.AddOp(3);
            target.Synced(0, false);
            WriteAndApplyDeltaTo(target);
            CollectionAssert.AreEqual(new[] { 20, 3, 4 }, target.Snapshot());
        }

        [Test]
        public void DeltaSync_Insert_RoundTrips()
        {
            _list.AddOp(1);
            _list.AddOp(2);
            _list.InsertOp(1, 99);

            TestIntSyncList target = WriteAndApplyDeltaTo(new TestIntSyncList());
            CollectionAssert.AreEqual(new[] { 1, 99, 2 }, target.Snapshot());
        }

        [Test]
        public void ReadSyncData_FiresOnOperationCallbacks()
        {
            _list.AddOp(42);

            LiteNetLibSyncListOp lastOp = LiteNetLibSyncListOp.Clear;
            int lastIndex = -1;
            int lastItem = 0;

            TestIntSyncList target = new TestIntSyncList();
            target.onOperation = (op, index, oldItem, newItem) =>
            {
                lastOp = op;
                lastIndex = index;
                lastItem = newItem;
            };
            WriteAndApplyDeltaTo(target);

            Assert.AreEqual(LiteNetLibSyncListOp.Add, lastOp);
            Assert.AreEqual(0, lastIndex);
            Assert.AreEqual(42, lastItem);
        }
    }
}
