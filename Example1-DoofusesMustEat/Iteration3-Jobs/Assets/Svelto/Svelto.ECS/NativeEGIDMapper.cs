using System;
using System.Runtime.CompilerServices;
using Svelto.DataStructures;

namespace Svelto.ECS
{
    public readonly struct NativeEGIDMapper<T>:IDisposable where T : unmanaged, IEntityStruct
    {
        readonly NativeFasterDictionaryStruct<uint, T> map;
        public ExclusiveGroupStruct groupID { get; }

        public NativeEGIDMapper(ExclusiveGroupStruct groupStructId, NativeFasterDictionaryStruct<uint, T> toNative):this()
        {
            groupID = groupStructId;
            map = toNative;
        }

        public uint Length => map.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Entity(uint entityID)
        {
            unsafe
            {
#if DEBUG && !PROFILER
                if (map.TryFindIndex(entityID, out var findIndex) == false)
                    throw new Exception("Entity not found in this group ".FastConcat(typeof(T).ToString()));
#else
                map.TryFindIndex(entityID, out var findIndex);
#endif
                return ref map.unsafeValues[(int) findIndex];
            }
        }
        
        public bool TryGetEntity(uint entityID, out T value)
        {
            if (map.TryFindIndex(entityID, out var index))
            {
                value = map.GetDirectValue(index);
                return true;
            }

            value = default;
            return false;
        }
        
        public unsafe NativeBuffer<T>GetArrayAndEntityIndex(uint entityID, out uint index)
        {
            if (map.TryFindIndex(entityID, out index))
            {
                return new NativeBuffer<T>(map.unsafeValues);
            }

            throw new ECSException("Entity not found");
        }
        
        public unsafe bool TryGetArrayAndEntityIndex(uint entityID, out uint index, out NativeBuffer<T> array)
        {
            if (map.TryFindIndex(entityID, out index))
            {
                array =  new NativeBuffer<T>(map.unsafeValues);
                return true;
            }

            array = default;
            return false;
        }

        public void Dispose()
        {
            map.Dispose();
        }
    }
}