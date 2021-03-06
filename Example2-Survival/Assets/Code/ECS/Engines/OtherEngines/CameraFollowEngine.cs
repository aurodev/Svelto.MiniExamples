using System.Collections;
using Svelto.Tasks;
using UnityEngine;

namespace Svelto.ECS.Example.Survive.Camera
{
    //First step identify the entity type we want the engine to handle: CameraEntity
    //Second step name the engine according the behaviour and the entity: I.E.: CameraFollowTargetEngine
    //Third step start to write the code and create classes/fields as needed using refactoring tools 
    public class CameraFollowTargetEngine: IQueryingEntitiesEngine
    {
        readonly ITaskRoutine<IEnumerator> _taskRoutine;

        readonly ITime _time;

        public CameraFollowTargetEngine(ITime time)
        {
            _time        = time;
        }

        public void Ready() { PhysicUpdate().RunOnScheduler(StandardSchedulers.physicScheduler); }

        public IEntitiesDB entitiesDB { get; set; }

        IEnumerator PhysicUpdate()
        {
            while (entitiesDB.HasAny<CameraTargetEntityView>(ECSGroups.CameraTarget) == false ||
                   entitiesDB.HasAny<CameraEntityView>(ECSGroups.ExtraStuff) == false)
                yield return null; //skip a frame

            var cameraTarget = entitiesDB.QueryUniqueEntity<CameraTargetEntityView>(ECSGroups.CameraTarget);
            var camera       = entitiesDB.QueryUniqueEntity<CameraEntityView>(ECSGroups.ExtraStuff);

            var smoothing = 5.0f;

            var offset = camera.positionComponent.position - cameraTarget.targetComponent.position;

            while (true)
            {
                var targetCameraPos = cameraTarget.targetComponent.position + offset;

                camera.transformComponent.position =
                    Vector3.Lerp(camera.positionComponent.position, targetCameraPos, smoothing * _time.deltaTime);

                yield return null;
            }
        }
    }
}