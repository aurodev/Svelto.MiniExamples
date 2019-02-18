using System;
using System.Collections.Generic;
using Svelto.DataStructures;
using Svelto.ECS.Internal;

namespace Svelto.ECS
{
    /// <summary>
    /// Do not use this class in place of a normal polling.
    /// I eventually realised than in ECS no form of communication other than polling entity components can exist.
    /// Using groups, you can have always an optimal set of entity components to poll, so EntityStreams must be used
    /// only if:
    /// - you want to polling engine to be able to track all the entity changes happening in between polls and not
    /// just the current state
    /// - you want a thread-safe way to read entity states, which includes all the state changes and not the last
    /// one only
    /// - you want to communicate between EnginesRoots  
    /// </summary>
    public class EntityStreams
    {
        public EntityStreams(IEntitiesDB entitiesDb)
        {
            _entitiesDB = entitiesDb;
        }

        public EntityStream<T>.Consumer GenerateConsumer<T>(int capacity) where T : unmanaged, IEntityStruct
        {
            if (_streams.ContainsKey(typeof(T)) == false) _streams[typeof(T)] = new EntityStream<T>();
            
            return (_streams[typeof(T)] as EntityStream<T>).GenerateConsumer(capacity);
        }

        public void PublishEntity<T>(EGID id) where T : unmanaged, IEntityStruct
        {
            if (_streams.TryGetValue(typeof(T), out var typeSafeStream)) 
                (typeSafeStream as EntityStream<T>).PublishEntity(ref _entitiesDB.QueryEntity<T>(id));
            else
                Console.LogWarning("No Consumers are waiting for this entity to change "
                                      .FastConcat(typeof(T).ToString()));
        }
        
        Dictionary<Type, ITypeSafeStream> _streams = new Dictionary<Type, ITypeSafeStream>();
        IEntitiesDB                        _entitiesDB;
    }

    interface ITypeSafeStream
    {}

    public class EntityStream<T>:ITypeSafeStream where T:unmanaged, IEntityStruct
    {
        public class Consumer
        {
            public Consumer(int capacity)
            {
                _ringBuffer = new RingBuffer<T>(capacity);
                _capacity = capacity;
            }

            public int Count => _ringBuffer.Count;

            public void Enqueue(ref T entity)
            {
                if (_ringBuffer.Count >= _capacity)
                    throw new Exception("EntityStream capacity has been saturated");
                
                _ringBuffer.Enqueue(ref entity);
            }

            readonly RingBuffer<T> _ringBuffer;
            int _capacity;

            public ref T Dequeue()
            {
                return ref _ringBuffer.Dequeue();
            }
        }
        
        public void PublishEntity(ref T entity)
        {
            for (int i = 0; i < _buffers.Count; i++)
                _buffers[i].Enqueue(ref entity);
        }

        public Consumer GenerateConsumer(int capacity)
        {
            var consumer = new Consumer(capacity);
            _buffers.Add(consumer);
            return consumer;
        }

        readonly FasterList<Consumer> _buffers = new FasterList<Consumer>();
    }
}    