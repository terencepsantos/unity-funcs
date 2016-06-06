using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
 
namespace UnityEngine.UI
{
	[AddComponentMenu("Event/Graphic Raycaster")]
	[RequireComponent(typeof(Canvas))]
	public class GraphicRaycaster : BaseRaycaster, ISerializationCallbackReceiver
	{
		protected const int kNoEventMaskSet = -1;
		public enum BlockingObjects
		{
			None = 0,
			TwoD = 1,
			ThreeD = 2,
			All = 3,
		}
 
		[SerializeField]
		private int m_Priority = -1;
 
		public override int priority
		{
			get 
			{ 
					return m_Priority;
			}
		}
 
		public bool ignoreReversedGraphics = true;
		public BlockingObjects blockingObjects = BlockingObjects.None;
 
		[SerializeField]
		protected LayerMask m_BlockingMask = kNoEventMaskSet;
 
		private Canvas m_Canvas;
 
		protected GraphicRaycaster()
		{}
 
		private Canvas canvas
		{
			get
			{
				if (m_Canvas != null)
					return m_Canvas;
 
				m_Canvas = GetComponent<Canvas>();
				return m_Canvas;
			}
		}
 
		[NonSerialized] private List<GameObject> m_RaycastResults = new List<GameObject>();
		public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
		{
			if (canvas == null || !canvas.receivesEvents)
				return;
 
			Vector2 pos;
			if (eventCamera == null)
				pos = new Vector2(eventData.position.x / Screen.width, eventData.position.y / Screen.height);
			else
				pos = eventCamera.ScreenToViewportPoint(eventData.position);
 
			// If it's outside the camera's viewport, do nothing
			if (pos.x < 0f || pos.x > 1f || pos.y < 0f || pos.y > 1f)
				return;
 
			float hitDistance = float.MaxValue;
 
			if (canvas.renderMode != RenderMode.Overlay && blockingObjects != BlockingObjects.None)
			{
				var ray = eventCamera.ScreenPointToRay(eventData.position);
				float dist = eventCamera.farClipPlane - eventCamera.nearClipPlane;
 
				if (blockingObjects == BlockingObjects.ThreeD || blockingObjects == BlockingObjects.All)
				{
					var hits = Physics.RaycastAll(ray, dist, m_BlockingMask);
 
					if (hits.Length > 0 && hits[0].distance < hitDistance)
					{
						hitDistance = hits[0].distance;
					}
				}
 
				if (blockingObjects == BlockingObjects.TwoD || blockingObjects == BlockingObjects.All)
				{
					var hits = Physics2D.GetRayIntersectionAll(ray, dist, m_BlockingMask);
 
					if (hits.Length > 0 && hits[0].fraction * dist < hitDistance)
					{
						hitDistance = hits[0].fraction * dist;
					}
				}
			}
 
			//Debug.Log("Trying canvas: " + m_Canvas.gameObject);
			m_RaycastResults.Clear();
			Raycast(canvas, eventCamera, eventData.position, m_RaycastResults);
 
			for (var index = 0; index < m_RaycastResults.Count; index++)
			{
				var go = m_RaycastResults[index];
				bool appendGraphic = true;
 
				if (ignoreReversedGraphics)
				{
					if (eventCamera == null)
					{
						// If we dont have a camera we know that we should always be facing forward
						var dir = go.transform.rotation * Vector3.forward;
						appendGraphic = Vector3.Dot(Vector3.forward, dir) > 0;
					}
					else
					{
						// If we have a camera compare the direction against the cameras forward.
						var cameraFoward = eventCamera.transform.rotation * Vector3.forward;
						var dir = go.transform.rotation * Vector3.forward;
						appendGraphic = Vector3.Dot(cameraFoward, dir) > 0;
					}
				}
 
				if (appendGraphic)
				{
					float distance = canvas.renderMode == RenderMode.Overlay ? 0 : Vector3.Distance(eventCamera.transform.position, canvas.transform.position);
 
					if (distance >= hitDistance)
						continue;
 
					var castResult = new RaycastResult
					{
						go = go,
						module = this,
						distance = distance,
						index = resultAppendList.Count
					};
					resultAppendList.Add(castResult);
				}
			}
		}
 
		public override Camera eventCamera
		{
			get 
			{
				if (canvas.renderMode == RenderMode.Overlay 
					|| (canvas.renderMode == RenderMode.OverlayCamera && canvas.worldCamera == null))
					return null;
 
				return canvas.worldCamera != null ? canvas.worldCamera : Camera.main; 
			}
		}
 
		public int defaultPriority
		{
			get { return 3; }
		}
 
		/// <summary>
		/// Perform a raycast into the screen and collect all graphics underneath it.
		/// </summary>
		[NonSerialized] static readonly List<Graphic> s_SortedGraphics = new List<Graphic>();
		private static void Raycast(Canvas canvas, Camera eventCamera, Vector2 pointerPosition, List<GameObject> results)
		{
			// Necessary for the event system
			var foundGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
			s_SortedGraphics.Clear();
 
			for (int i = 0; i < foundGraphics.Count; ++i)
			{
				Graphic graphic = foundGraphics[i];
 
				// -1 means it hasn't been processed by the canvas, which means it isn't actually drawn
				if (graphic.depth == -1)
					continue;
				if (!RectTransformUtility.PointInRectangle (pointerPosition, graphic.rectTransform, eventCamera))
					continue;
				
				if (graphic.Raycast(pointerPosition, eventCamera))
				{
						s_SortedGraphics.Add(graphic);
				}
			}
 
			s_SortedGraphics.Sort((g1, g2) => g2.depth.CompareTo(g1.depth));
			for (int i = 0; i < s_SortedGraphics.Count; ++i)
				results.Add(s_SortedGraphics[i].gameObject);
		}
 
		#region serialization callback
 
		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{ }
 
		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if (m_Priority <= 0)
			{
				m_Priority = defaultPriority;
			}
		}
		#endregion
	}
}