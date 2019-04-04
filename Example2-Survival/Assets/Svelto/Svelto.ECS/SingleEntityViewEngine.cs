using System;
using Svelto.ECS.Internal;

namespace Svelto.ECS
{
    [Obsolete]
    public abstract class SingleEntityViewEngine<T> : EngineInfo, IHandleEntityStructEngine<T> where T : class, IEntityStruct
    {
        public void AddInternal(ref T entityView, ExclusiveGroup.ExclusiveGroupStruct? previousGroup)
        { Add(entityView); }

        public void RemoveInternal(ref T entityView, bool itsaSwap)
        { Remove(entityView); }

        protected abstract void Add(T entityView);
        protected abstract void Remove(T entityView);
    }
}