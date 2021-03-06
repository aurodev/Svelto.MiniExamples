using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Svelto.DataStructures;

namespace Svelto.ECS
{
    public ref struct EntityCollection<T> where T : IEntityStruct
    {
        public EntityCollection(T[] array, uint count) : this()
        {
            _buffer.Set(array);
            _count = count;
        }

        public EntityCollection(ManagedBuffer<T> buffer, uint count)
        {
            _buffer = buffer;
            _count = count;
        }

        public uint length => _count;

        readonly ManagedBuffer<T> _buffer;
        readonly uint             _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToFastAccess(out uint actualCount)
        {
            actualCount = _count;
            return _buffer.ToManagedArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeBuffer<NT> ToNativeBuffer<NT>(out uint length) where NT : unmanaged, T
        {
            length = _count;
            return new NativeBuffer<NT>(Unsafe.As<NT[]>(_buffer.ToManagedArray()));
        }
        
        public EntityNativeIterator<NT> GetNativeEnumerator<NT>() where NT : unmanaged, T 
                    { return new EntityNativeIterator<NT>(ToNativeBuffer<NT>(out _)); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ManagedBuffer<T> ToBuffer(out uint length)
        {
            length = _count;
            return _buffer;
        }

        public ref T this[uint i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _buffer[i];
        }

        public ref T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _buffer[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityIterator GetEnumerator()
        {
            return new EntityIterator(_buffer, _count);
        }

        public struct EntityIterator
        {
            public EntityIterator(ManagedBuffer<T> array, uint count) : this()
            {
                _array = array.ToManagedArray();
                _count = count;
                _index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++_index < _count;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _array[_index];
            }

            readonly T[]  _array;
            readonly uint _count;
            int           _index;
        }
        
        /// <summary>
        /// Note: this Enumerator is designed to work in a multithreaded parallel environment. The enumerator
        /// can then be copied over several threads, that's why it must operate on pointers, otherwise each
        /// thread will have it's own index which is not the goal of this enumerator.
        /// </summary>
        /// <typeparam name="NT"></typeparam>
        public struct EntityNativeIterator<NT>:IDisposable where NT : unmanaged
        {
            public EntityNativeIterator(NativeBuffer<NT> array) : this()
            {
                unsafe
                {
                    _array = array;
                    _index = (int*) Marshal.AllocHGlobal(sizeof(int));
                    *_index = -1;
                }
            }

            public ref NT threadSafeNext
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    unsafe
                    {
                        return ref ((NT*) _array.ToNativeArray())[Interlocked.Increment(ref *_index)];
                    }
                }
            }

            public ref readonly NT current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    unsafe
                    {
                        return ref ((NT*) _array.ToNativeArray())[*_index];
                    }
                }
            }

            public void Dispose() {
                unsafe
                {
                    _array.Dispose(); Marshal.FreeHGlobal((IntPtr) _index);
                }
            }

            readonly NativeBuffer<NT> _array;
#if ENABLE_BURST_AOT        
            [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif
            readonly unsafe int *          _index;
        }
    }

    public ref struct EntityCollection<T1, T2>
        where T1 : IEntityStruct where T2 : IEntityStruct
    {
        public EntityCollection(in EntityCollection<T1> array1, in EntityCollection<T2> array2)
        {
            _array1 = array1;
            _array2 = array2;
        }

        public uint length => _array1.length;

        public EntityCollection<T2> Item2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array2;
        }

        public EntityCollection<T1> Item1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array1;
        }

        readonly EntityCollection<T1> _array1;
        readonly EntityCollection<T2> _array2;

        public (T1[], T2[]) ToFastAccess(out uint count)
        {
            count = length;

            return (_array1.ToFastAccess(out _), _array2.ToFastAccess(out _));
        }

        public BufferTuple<ManagedBuffer<T1>, ManagedBuffer<T2>> ToBuffers()
        {
            var bufferTuple = new BufferTuple<ManagedBuffer<T1>, ManagedBuffer<T2>>
                (_array1.ToBuffer(out _), _array2.ToBuffer(out _), length);
            return bufferTuple;
        }

        public BufferTuple<NativeBuffer<NT1>, NativeBuffer<NT2>> ToNativeBuffers<NT1, NT2>()
            where NT2 : unmanaged, T2 where NT1 : unmanaged, T1
        {
            var bufferTuple = new BufferTuple<NativeBuffer<NT1>, NativeBuffer<NT2>>
                (_array1.ToNativeBuffer<NT1>(out _), _array2.ToNativeBuffer<NT2>(out _), length);

            return bufferTuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityIterator GetEnumerator()
        {
            return new EntityIterator(this);
        }

        public ref struct EntityIterator
        {
            public EntityIterator(in EntityCollection<T1, T2> array1) : this()
            {
                _array1 = array1;
                _count = array1.length;
                _index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++_index < _count;
            }

            public void Reset()
            {
                _index = -1;
            }

            public ValueRef<T1, T2> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new ValueRef<T1, T2>(_array1, (uint) _index);
            }

            readonly EntityCollection<T1, T2> _array1;
            readonly uint                 _count;
            int                           _index;
        }
    }

    public ref struct EntityCollection<T1, T2, T3> 
        where T3 : IEntityStruct where T2 : IEntityStruct where T1 : IEntityStruct
    {
        public EntityCollection(
            in EntityCollection<T1> array1, in EntityCollection<T2> array2,
            in EntityCollection<T3> array3)
        {
            _array1 = array1;
            _array2 = array2;
            _array3 = array3;
        }

        public EntityCollection<T1> Item1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array1;
        }

        public EntityCollection<T2> Item2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array2;
        }

        public EntityCollection<T3> Item3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array3;
        }

        public uint length => Item1.length;

        public (T1[], T2[], T3[]) ToFastAccess(out uint count)
        {
            count = length;

            return (_array1.ToFastAccess(out _), _array2.ToFastAccess(out _), _array3.ToFastAccess(out _));
        }

        public BufferTuple<ManagedBuffer<T1>, ManagedBuffer<T2>, ManagedBuffer<T3>> ToBuffers()
        {
            var bufferTuple = new BufferTuple<ManagedBuffer<T1>, ManagedBuffer<T2>, ManagedBuffer<T3>>
                (_array1.ToBuffer(out _), _array2.ToBuffer(out _), _array3.ToBuffer(out _), length);
            return bufferTuple;
        }

        public BufferTuple<NativeBuffer<NT1>, NativeBuffer<NT2>, NativeBuffer<NT3>> ToNativeBuffers<NT1, NT2, NT3>()
            where NT2 : unmanaged, T2 where NT1 : unmanaged, T1 where NT3 : unmanaged, T3
        {
            var bufferTuple = new BufferTuple<NativeBuffer<NT1>, NativeBuffer<NT2>, NativeBuffer<NT3>>
            (_array1.ToNativeBuffer<NT1>(out _), _array2.ToNativeBuffer<NT2>(out _),
                _array3.ToNativeBuffer<NT3>(out _), length);

            return bufferTuple;
        }

        readonly EntityCollection<T1> _array1;
        readonly EntityCollection<T2> _array2;
        readonly EntityCollection<T3> _array3;
    }

    public ref struct EntityCollections<T> where T : struct, IEntityStruct
    {
        public EntityCollections(IEntitiesDB db, ExclusiveGroup[] groups) : this()
        {
            _db = db;
            _groups = groups;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityGroupsIterator GetEnumerator()
        {
            return new EntityGroupsIterator(_db, _groups);
        }

        readonly IEntitiesDB      _db;
        readonly ExclusiveGroup[] _groups;

        public ref struct EntityGroupsIterator
        {
            public EntityGroupsIterator(IEntitiesDB db, ExclusiveGroup[] groups) : this()
            {
                _db = db;
                _groups = groups;
                _indexGroup = -1;
                _index = -1;
            }

            public bool MoveNext()
            {
                //attention, the while is necessary to skip empty groups
                while (_index + 1 >= _count && ++_indexGroup < _groups.Length)
                {
                    _index = -1;
                    _array = _db.QueryEntities<T>(_groups[_indexGroup]);
                    _count = _array.length;
                }

                return ++_index < _count;
            }

            public void Reset()
            {
                _index = -1;
                _indexGroup = -1;
                _count = 0;
            }

            public ref T Current => ref _array[(uint) _index];

            readonly IEntitiesDB      _db;
            readonly ExclusiveGroup[] _groups;

            EntityCollection<T> _array;
            uint                _count;
            int                 _index;
            int                 _indexGroup;
        }
    }

    public ref struct EntityCollections<T1, T2>
        where T1 : struct, IEntityStruct where T2 : struct, IEntityStruct
    {
        public EntityCollections(IEntitiesDB db, ExclusiveGroup[] groups) : this()
        {
            _db = db;
            _groups = groups;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityGroupsIterator GetEnumerator()
        {
            return new EntityGroupsIterator(_db, _groups);
        }

        readonly IEntitiesDB      _db;
        readonly ExclusiveGroup[] _groups;

        public ref struct EntityGroupsIterator
        {
            public EntityGroupsIterator(IEntitiesDB db, ExclusiveGroup[] groups) : this()
            {
                _db = db;
                _groups = groups;
                _indexGroup = -1;
                _index = -1;
            }

            public bool MoveNext()
            {
                //attention, the while is necessary to skip empty groups
                while (_index + 1 >= _array1.length && ++_indexGroup < _groups.Length)
                {
                    _index = -1;
                    _array1 = _db.QueryEntities<T1, T2>(_groups[_indexGroup]);
                }

                return ++_index < _array1.length;
            }

            public void Reset()
            {
                _index = -1;
                _indexGroup = -1;

                _array1 = _db.QueryEntities<T1, T2>(_groups[0]);
            }

            public ValueRef<T1, T2> Current
            {
                get
                {
                    var valueRef =
                        new ValueRef<T1, T2>(_array1, (uint) _index);
                    return valueRef;
                }
            }

            readonly IEntitiesDB      _db;
            readonly ExclusiveGroup[] _groups;
            int                       _index;
            int                       _indexGroup;

            EntityCollection<T1, T2> _array1;
        }
    }
    
    public ref struct EntityCollections<T1, T2, T3>
        where T1 : struct, IEntityStruct where T2 : struct, IEntityStruct where T3 : struct, IEntityStruct
    {
        public EntityCollections(IEntitiesDB db, ExclusiveGroup[] groups) : this()
        {
            _db = db;
            _groups = groups;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityGroupsIterator GetEnumerator()
        {
            return new EntityGroupsIterator(_db, _groups);
        }

        readonly IEntitiesDB      _db;
        readonly ExclusiveGroup[] _groups;

        public ref struct EntityGroupsIterator
        {
            public EntityGroupsIterator(IEntitiesDB db, ExclusiveGroup[] groups) : this()
            {
                _db = db;
                _groups = groups;
                _indexGroup = -1;
                _index = -1;
            }

            public bool MoveNext()
            {
                //attention, the while is necessary to skip empty groups
                while (_index + 1 >= _count && ++_indexGroup < _groups.Length)
                {
                    _index = -1;
                    _array1 = _db.QueryEntities<T1, T2, T3>(_groups[_indexGroup]);
                    _count = _array1.length;

                }

                return ++_index < _count;
            }

            public void Reset()
            {
                _index = -1;
                _indexGroup = -1;

                _array1 = _db.QueryEntities<T1, T2, T3>(_groups[0]);
                _count = _array1.length;
            }

            public ValueRef<T1, T2, T3> Current
            {
                get
                {
                    var valueRef =
                        new ValueRef<T1, T2, T3>(_array1, (uint) _index);
                    return valueRef;
                }
            }

            readonly IEntitiesDB      _db;
            readonly ExclusiveGroup[] _groups;
            uint                      _count;
            int                       _index;
            int                       _indexGroup;

            EntityCollection<T1, T2, T3> _array1;
        }
    }

    public readonly struct BufferTuple<BufferT1, BufferT2, BufferT3> : IDisposable where BufferT1 : IDisposable
                                                                                   where BufferT2 : IDisposable
                                                                                   where BufferT3 : IDisposable
    {
        public readonly BufferT1 buffer1;
        public readonly BufferT2 buffer2;
        public readonly BufferT3 buffer3;
        public readonly uint     count;

        public BufferTuple(BufferT1 bufferT1, BufferT2 bufferT2, BufferT3 bufferT3, uint count) : this()
        {
            this.buffer1 = bufferT1;
            this.buffer2 = bufferT2;
            this.buffer3 = bufferT3;
            this.count = count;
        }

        public void Dispose()
        {
            buffer1.Dispose();
            buffer2.Dispose();
            buffer3.Dispose();
        }
    }

    public readonly struct BufferTuple<BufferT1, BufferT2> : IDisposable
        where BufferT1 : IDisposable where BufferT2 : IDisposable
    {
        public readonly BufferT1 buffer1;
        public readonly BufferT2 buffer2;
        public readonly uint count;

        public BufferTuple(BufferT1 bufferT1, BufferT2 bufferT2, uint count) : this()
        {
            this.buffer1 = bufferT1;
            this.buffer2 = bufferT2;
            this.count = count;
        }

        public void Dispose()
        {
            buffer1.Dispose();
            buffer2.Dispose();
        }
    }

    public ref struct ValueRef<T1, T2> where T2 : IEntityStruct where T1 : IEntityStruct
    {
        readonly EntityCollection<T1, T2> array1;

        readonly uint index;

        public ValueRef(in EntityCollection<T1, T2> entity2, uint i)
        {
            array1 = entity2;
            index = i;
        }

        public ref T1 entityStructA
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array1.Item1[index];
        }

        public ref T2 entityStructB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array1.Item2[index];
        }
    }

    public ref struct ValueRef<T1, T2, T3> 
        where T2 : IEntityStruct where T1 : IEntityStruct where T3 : IEntityStruct
    {
        readonly EntityCollection<T1, T2, T3> array1;

        readonly uint index;

        public ValueRef(in EntityCollection<T1, T2, T3> entity, uint i)
        {
            array1 = entity;
            index  = i;
        }

        public ref T1 entityStructA
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array1.Item1[index];
        }

        public ref T2 entityStructB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array1.Item2[index];
        }
        
        public ref T3 entityStructC
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array1.Item3[index];
        }
    }
}