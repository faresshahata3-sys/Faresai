using UnityEngine;
using UnityEngine.Rendering;

namespace StreetRush
{
    /// <summary>
    /// Renders a sky-only reflection cubemap once at startup so glossy car
    /// paint, glass and water reflect the sky without any light baking.
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    public class SkyReflection : MonoBehaviour
    {
        public bool includeScene;

        void Start()
        {
            var probe = GetComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(6000, 2000, 6000);
            probe.resolution = 128;
            probe.hdr = true;
            probe.boxProjection = false;
            probe.importance = 0;
            probe.cullingMask = includeScene ? ~0 : 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.RenderProbe();
        }
    }
}
