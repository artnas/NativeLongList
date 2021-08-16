using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeLongList
{
	/// <summary>
	/// A version of NativeList that supports large amounts of data (above 1 GB)
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of elements in the list. Must be blittable.
	/// </typeparam>
	///
	/// <author>
	/// Made by Artur Nasiadko, https://github.com/artnas
	/// Based on NativeCustomArray by Jackson Dunstan, http://JacksonDunstan.com/articles/4734
	/// </author>
	[NativeContainer]
	public unsafe struct NativeLongList<T> : IDisposable
		where T : unmanaged
	{
		// internal data
		[NativeDisableUnsafePtrRestriction]
		private NativeListData<T>* _data;
		
		private bool _isCreated;
		private Allocator _allocator;

		// These are all required when checks are enabled
		// They must have these exact types, names, and attributes
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
		private AtomicSafetyHandle m_Safety;
		[NativeSetClassTypeToNullOnSchedule] private DisposeSentinel MDisposeSentinel;
	#endif
	 
		public NativeLongList(Allocator allocator) : this(64, allocator)
		{
		}

		public NativeLongList(long capacity, Allocator allocator)
		{
			_isCreated = true;
			_allocator = allocator;

			// allocate list data
			_data = AllocatorManager.Allocate<NativeListData<T>>(new AllocatorManager.AllocatorHandle { Value = (int)allocator });
			_data->Capacity = capacity;
			_data->Count = 0;
			
			var totalSize = UnsafeUtility.SizeOf<T>() * capacity;

			// allocate list buffer
			_data->Buffer = (T*) UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
			_data->BufferLength = totalSize;

			// Initialize fields for safety checks
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Create(out m_Safety, out MDisposeSentinel, 0, allocator);
	#endif
		}

		public long Capacity => _data->Capacity;
		
		public long Length => _data->Count;

		public bool IsCreated => _isCreated;

		public void Clear()
		{
			_data->Count = 0;
		}
		
		public void Dispose()
		{
			if (!_isCreated || _data == null)
				return;

			_isCreated = false;
			
			UnsafeUtility.Free(_data->Buffer, _allocator);
			UnsafeUtility.Free(_data, _allocator);
			
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Dispose(ref m_Safety, ref MDisposeSentinel);
#endif
		}
		
		public T this[long index]
		{
			get => *&_data->Buffer[index];
			set => *&_data->Buffer[index] = value;
		}
		
		public void Add(T value)
		{
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			
			ResizeIfNecessary(1);

			// Write new value at the end
			*&_data->Buffer[_data->Count] = value;

			// Count the newly-added element
			_data->Count++;
		}

		public void AddRange(void* buffer, long elementsCount)
		{
			var bufferLength = elementsCount * UnsafeUtility.SizeOf<T>();
		
			ResizeIfNecessary(elementsCount);

			// add elements to end of array
			UnsafeUtility.MemCpy(&_data->Buffer[_data->Count], buffer, bufferLength);

			_data->Count += elementsCount;
		}

		private void ResizeIfNecessary(long requiredAdditionalCapacity)
		{
			if (_data->Count + requiredAdditionalCapacity < _data->Capacity)
				return;
			
			var sizeOfType = UnsafeUtility.SizeOf<T>();

			var newLength = _data->BufferLength * 2;
			var newCapacity = newLength / sizeOfType;

			// Add some additional capacity
			var capacityDiff = requiredAdditionalCapacity - newCapacity;
			if (capacityDiff > 0)
			{
				newLength += capacityDiff * sizeOfType;
			}

			var newBuffer = (T*)UnsafeUtility.Malloc(newLength, UnsafeUtility.AlignOf<T>(), _allocator);
			
			// copy existing memory
			UnsafeUtility.MemCpy(newBuffer, _data->Buffer, _data->BufferLength);

			// free old buffer
			UnsafeUtility.Free(_data->Buffer, _allocator);

			_data->Capacity = newLength / sizeOfType;
			_data->BufferLength = newLength;
			_data->Buffer = newBuffer;
		}

		public void RemoveAt(int index)
		{
			long numElementsToShift = _data->Count - index - 1;

			if (numElementsToShift > 0)
			{
				int elementSize = UnsafeUtility.SizeOf<T>();
				byte* source = (byte*)_data->Buffer + elementSize * (index + 1);
				long shiftSize = numElementsToShift * elementSize;
				UnsafeUtility.MemMove(source - elementSize, source, shiftSize);
			}
	 
			_data->Count--;
		}
		
		/// <summary>
		/// Trims unused excess memory with an option to retain some additional capacity
		/// </summary>
		/// <param name="leaveSomeAdditionalCapacity"></param>
		public void TrimExcess(bool leaveSomeAdditionalCapacity = false)
		{
			var sizeOfType = UnsafeUtility.SizeOf<T>();
			
			var newBufferLength = _data->Count * sizeOfType;
			
			// add 10% additional capacity
			if (leaveSomeAdditionalCapacity)
				newBufferLength += UnityEngine.Mathf.RoundToInt(newBufferLength * 0.1f);
			
			if (_data->BufferLength > newBufferLength)
			{
				var existingBuffer = _data->Buffer;
				var newBuffer = (T*)UnsafeUtility.Malloc(newBufferLength, UnsafeUtility.AlignOf<T>(), _allocator);
				
				// copy existing memory
				UnsafeUtility.MemCpy(newBuffer, _data->Buffer, _data->Count * sizeOfType);

				// free old buffer
				UnsafeUtility.Free(existingBuffer, _allocator);
			
				_data->Capacity = newBufferLength / sizeOfType;
				_data->BufferLength = newBufferLength;
				_data->Buffer = newBuffer;
			}
		}
	}

	internal struct NativeListData<T> where T : unmanaged
	{
		public long Count;
		public long Capacity;
		public long BufferLength;
		[NativeDisableUnsafePtrRestriction]
		public unsafe T* Buffer;
	}
}