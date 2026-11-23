using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StreetRush
{
    /// <summary>
    /// Multi-touch button: every pointer is tracked independently, so e.g.
    /// GAS + LEFT, or joystick + RUN, can be held at the same time.
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Down;
        public event Action Up;
        public Image background;
        readonly HashSet<int> _pointers = new HashSet<int>();
        public bool IsHeld => _pointers.Count > 0;

        public void OnPointerDown(PointerEventData e)
        {
            bool was = IsHeld;
            _pointers.Add(e.pointerId);
            if (!was) { Down?.Invoke(); SetVisual(true); }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_pointers.Remove(e.pointerId)) return;
            if (!IsHeld) { Up?.Invoke(); SetVisual(false); }
        }

        void OnDisable()
        {
            if (IsHeld) { _pointers.Clear(); Up?.Invoke(); SetVisual(false); }
        }

        void SetVisual(bool pressed)
        {
            transform.localScale = pressed ? Vector3.one * 0.92f : Vector3.one;
            if (background) background.color = pressed ? new Color(1f, 0.76f, 0.12f, 0.6f) : new Color(0.05f, 0.06f, 0.08f, 0.55f);
        }
    }

    /// <summary>Floating virtual joystick: appears where the thumb lands inside its zone.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform baseRing, knob;
        public float radius = 90f;
        public Vector2 Output { get; private set; }   // x = right, y = forward (up)
        int _pointer = int.MinValue;
        Vector2 _restPos;
        RectTransform _rt;

        void Start()
        {
            // Start (not Awake): baseRing/knob are assigned right after AddComponent.
            _rt = (RectTransform)transform;
            _restPos = baseRing.anchoredPosition;
        }

        Vector2 ToLocal(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, null, out var local);
            return local;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue) return;
            _pointer = e.pointerId;
            baseRing.localPosition = ToLocal(e);
            knob.localPosition = baseRing.localPosition;
            UpdateKnob(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            UpdateKnob(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            Release();
        }

        void Release()
        {
            _pointer = int.MinValue;
            Output = Vector2.zero;
            baseRing.anchoredPosition = _restPos;
            knob.anchoredPosition = _restPos;
        }

        void OnDisable() { if (_pointer != int.MinValue) Release(); }

        void UpdateKnob(PointerEventData e)
        {
            Vector2 center = baseRing.localPosition;
            Vector2 d = ToLocal(e) - center;
            if (d.magnitude > radius) d = d.normalized * radius;
            knob.localPosition = center + d;
            Vector2 o = d / radius;
            Output = o.magnitude < 0.12f ? Vector2.zero : o;
        }
    }

    /// <summary>Transparent full-screen area: drags rotate the camera (any free part of the screen).</summary>
    public class CameraLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e) { }
        public void OnPointerUp(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            // Normalise to a 1280x720 reference so sensitivity is the same on every phone.
            float scale = 720f / Mathf.Max(1f, Screen.height);
            MobileInput.LookDelta += e.delta * scale;
        }
    }
}
