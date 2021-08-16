using System;
using System.Collections.Generic;
using System.Diagnostics;
using NativeLongList;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Testing
{
    public class TestRunner : MonoBehaviour
    {
        [Range(1, 10)]
        public int LoopsCount = 10;
        
        private readonly Stopwatch _stopwatch = new();
        private readonly Dictionary<string, double> _actionTimes = new();
        
        void Start()
        {
            for (var i = 0; i < LoopsCount + 1; i++)
            {
                // dont track time of first loop - burst is always slower first time its run, add 1 additional loop
                var addTimeToRecord = i > 0;
                
                TestCreation(addTimeToRecord);
                TestAdd(addTimeToRecord);
                TestAddRange(addTimeToRecord);
            }
            
            LogActionTimes();
            
            _actionTimes.Clear();
        }

        private void LogActionTimes()
        {
            foreach (var (actionName, ms) in _actionTimes)
            {
                var avgTime = ms / LoopsCount;
                Debug.Log($"{actionName}: avg {avgTime:n2} ms ({LoopsCount} samples)");
            }
        }
        
        private void DoActionMeasureTime(string actionName, bool addTimeToRecord, Action action)
        {
            _stopwatch.Restart();
            action();
            _stopwatch.Stop();

            if (!addTimeToRecord)
                return;
            
            var ms = _stopwatch.Elapsed.TotalMilliseconds;

            if (_actionTimes.TryGetValue(actionName, out var existingTime))
            {
                _actionTimes[actionName] = existingTime + ms;
            }
            else
            {
                _actionTimes.Add(actionName, ms);
            }
        }

#region Creation

        public void TestCreation(bool addTimeToRecord)
        {
            const int maxSize = int.MaxValue / 8;
            
            DoActionMeasureTime($"base list create with {maxSize} capacity", addTimeToRecord, () =>
            {
                var nativeList = new NativeList<int>(maxSize, Allocator.Persistent);
                nativeList.Dispose();
            });
            
            DoActionMeasureTime($"long list create with {maxSize} capacity", addTimeToRecord, () =>
            {
                var nativeLongList = new NativeLongList<int>(maxSize, Allocator.Persistent);
                nativeLongList.Dispose();
            });
        }
        
        #endregion

#region Test Add & Read

        public void TestAdd(bool addTimeToRecord)
        {
            TestAddCustomCount(1000000, addTimeToRecord);
            
            const int maxSize = int.MaxValue / 8;
            TestAddCustomCount(maxSize, addTimeToRecord);
        }

        private void TestAddCustomCount(int count, bool addTimeToRecord)
        {
            DoActionMeasureTime($"base list add & read {count} elements", addTimeToRecord, () =>
            {
                var nativeList = new NativeList<int>(10, Allocator.Persistent);
                for (var i = 0; i < count; i++)
                {
                    nativeList.Add(i);
                    if (nativeList[i] != i)
                        throw new Exception($"incorrect value at {i}");
                }
                nativeList.Dispose();
            });
            
            DoActionMeasureTime($"base list add {count} elements (burst)", addTimeToRecord, () =>
            {
                var nativeList = new NativeList<int>(10, Allocator.Persistent);
                new NativeListAddJob
                {
                    Count = count,
                    List = nativeList
                }.Schedule().Complete();
                nativeList.Dispose();
            });
            
            DoActionMeasureTime($"long list add & read {count} elements", addTimeToRecord, () =>
            {
                var nativeLongList = new NativeLongList<int>(10, Allocator.Persistent);
                for (var i = 0; i < count; i++)
                {
                    nativeLongList.Add(i);
                    if (nativeLongList[i] != i)
                        throw new Exception($"incorrect value at {i}");
                }
                nativeLongList.Dispose();
            });
            
            DoActionMeasureTime($"long list add {count} elements (burst)", addTimeToRecord, () =>
            {
                var nativeLongList = new NativeLongList<int>(10, Allocator.Persistent);
                new NativeLongListAddJob
                {
                    Count = count,
                    List = nativeLongList
                }.Schedule().Complete();
                nativeLongList.Dispose();
            });
        }

        [BurstCompile]
        private struct NativeListAddJob: IJob
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeList<int> List;
            public int Count;
            
            public void Execute()
            {
                for (var i = 0; i < Count; i++)
                {
                    List.Add(i);
                }
            }
        }
        
        [BurstCompile]
        private struct NativeLongListAddJob: IJob
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeLongList<int> List;
            public int Count;
            
            public void Execute()
            {
                for (var i = 0; i < Count; i++)
                {
                    List.Add(i);
                }
            }
        }

#endregion

#region Test Add Range

        private unsafe void TestAddRange(bool addTimeToRecord)
        {
            const int timesToAdd = 10;
            
            const int bufferSize = 1000000;
            var elementsCount = bufferSize / UnsafeUtility.SizeOf<int>();
            
            var buffer = UnsafeUtility.Malloc(bufferSize, UnsafeUtility.AlignOf<int>(), Allocator.Persistent);
            
            DoActionMeasureTime($"base list add range {elementsCount} elements", addTimeToRecord, () =>
            {
                var nativeList = new NativeList<int>(10, Allocator.Persistent);
                for (var i = 0; i < timesToAdd; i++)
                {
                    nativeList.AddRange(buffer, elementsCount);
                }
                nativeList.Dispose();
            });

            DoActionMeasureTime($"long list add range {elementsCount} elements", addTimeToRecord, () =>
            {
                var nativeLongList = new NativeLongList<int>(10, Allocator.Persistent);
                for (var i = 0; i < timesToAdd; i++)
                {
                    nativeLongList.AddRange(buffer, elementsCount);
                }
                nativeLongList.Dispose();
            });
            
            UnsafeUtility.Free(buffer, Allocator.Persistent);
        }

#endregion

    }
}
