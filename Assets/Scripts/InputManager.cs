using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// handles input, forwards interactions to provided callback functions
public class InputManager : PersistentMonoBehaviorSingleton<InputManager> {
	
	// maximum number of touches to reach
	private static int maxTouches = 5;
	
	// time between tap and release, or double click/tap
	private static float interactTimeGap = 0.2f;
	
	// types of interaction
	public enum InteractionType {
		onMouseLeftDown,
		onMouseLeftUp,
		onMouseLeftClick,
		onMouseLeftDoubleClick,
		
		onMouseRightDown,
		onMouseRightUp,
		onMouseRightClick,
		onMouseRightDoubleClick,
		
		onTouchStart,
		onTouchEnd,
		onTap,
		onDoubleTap,
		
		onPress,
		onRelease,
		onClick,
		onDoubleClick
	};
	
	// custom type to keep track of objects each pointer interacted with
	private class PointerObjectMap {
		public Vector3 pointer;
		public List<GameObject> objs;
	};
	
	// the number of touches currently interacting with the device
	public int numTouches { get; private set; }
	
	// interacting objects
	private Dictionary<GameObject, Vector3> interactObjects;
	
	// pointer list and their mapped objects
	private SortedList<int, PointerObjectMap> pointers;
	
	// interact vars
	public bool mouseLeftDown { get; private set; }
	public bool mouseRightDown { get; private set; }
	public bool touchStart { get; private set; }
	
	private int leftClickCount = 0;
	private int rightClickCount = 0;
	private int tapCount = 0;
	private float singleTimer = 0;
	private float doubleTimer = 0;
	
	// initialization
	private InputManager() {
		this.interactObjects = new Dictionary<GameObject, Vector3>();
		this.pointers = new SortedList<int, PointerObjectMap>();
		this.mouseLeftDown = false;
		this.mouseRightDown = false;
		this.touchStart = false;
	}
	
	// the most recently interacted with object
	public GameObject lastInteracted { get; private set; }
	
	// is the object being interacted with
	public bool isInteracting(GameObject go) {
		return this.interactObjects.ContainsKey(go);
	}
	
	// gets the position of the pointer
	public Vector3 getPosition(int index = 0) {
		Vector3 point = new Vector3();
		if(this.pointers.ContainsKey(index)) {
			point = this.convertToWorldPoint(this.pointers[index].pointer);
		}
		return point;
	}	
	
	// gets the pointer that this object is attached to
	public Vector3 getPosition(GameObject go) {
		Vector3 point = new Vector3();
		if(this.interactObjects.ContainsKey(go)) {
			point = this.interactObjects[go];
		}
		return this.convertToWorldPoint(point);
	}
	
	// copies an interaction event from one game object to another
	public void spawnInteraction(GameObject from, GameObject to) {
		for(int i = 0; i < this.pointers.Count; i++) {
			if(this.pointers[i].objs.Contains(from)) {
				this.updatePointer(i, this.pointers[i].pointer, to);
				this.refreshInteractObjects();
				
				#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
					this.onTouchStart(to);
				#else
					this.onMouseLeftDown(to);
				#endif
			}
		}
	}
	
	// update interaction data
	private void Update() {
	
		// update timers
		this.singleTimer += Time.deltaTime;
		this.doubleTimer += Time.deltaTime;
		if(this.doubleTimer > InteractiveBehavior.interactTimeGap) {
			this.leftClickCount = 0;
			this.rightClickCount = 0;
			this.tapCount = 0;
		}
	
		#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
			this.handleTouchEvents();
		#else
			this.handleMouseEvents();
		#endif
	}
	
	// process touch events
	private void handleTouchEvents() {
		
		int numTouches = Input.touchCount;
		if(numTouches > 0) {
			
			for(var i = 0; i < numTouches && i < InputManager.maxTouches; ++i) {
				
				Vector3 point = Input.GetTouch(i).position;
				TouchPhase phase = Input.GetTouch(i).phase;
				GameObject go = this.getFirstGameObjectHit(point);
				int pointerIndex = this.updatePointer(i, point, go);
				
				if(phase == TouchPhase.Began) {
					this.onTouchStart(go);
				} else if(phase == TouchPhase.Ended || phase == TouchPhase.Canceled) {
					this.onTouchEnd(go, pointerIndex);
				}
			}
			
			this.refreshInteractObjects();
		}
	}
	
	// process mouse events
	private void handleMouseEvents() {
		
		Vector3 point = Input.mousePosition;
		int pointerIndex = 0;
		GameObject go = null;
		
		if(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonUp(0) || Input.GetMouseButton(1)) {
			go = this.getFirstGameObjectHit(point);
		}
		
		this.updatePointer(pointerIndex, point, go);
		this.refreshInteractObjects();
		
		if (Input.GetMouseButtonDown(0)) {
			this.onMouseLeftDown(go);
		} else if(Input.GetMouseButtonUp(0)) {
			this.onMouseLeftUp(go, pointerIndex);
		}
		
		if(Input.GetMouseButtonDown(1)) {
			this.onMouseRightDown(go);
		} else if(Input.GetMouseButtonUp(1)) {
			this.onMouseRightUp(go, pointerIndex);
		}
	}
	
	private void onMouseLeftDown(GameObject go) {
		this.mouseLeftDown = true;
		this.singleTimer = 0;
		
		this.notifyGameObject(go, InteractionType.onMouseLeftDown);
		this.notifyGameObject(go, InteractionType.onPress);
		/*this.broadcastInteraction(go, InteractionType.onMouseLeftDown);
		this.broadcastInteraction(go, InteractionType.onPress);*/
	}
	
	private void onMouseLeftUp(GameObject go, int pointerIndex) {
		this.mouseLeftDown = false;
		this.doubleTimer = 0;
		this.leftClickCount++;
		
		
		/*this.broadcastInteraction(go, InteractionType.onMouseLeftUp);
		this.broadcastInteraction(go, InteractionType.onRelease);*/
		
		if(this.leftClickCount >= 2) {
			this.notifyGameObject(go, InteractionType.onMouseLeftDoubleClick);
			this.notifyGameObject(go, InteractionType.onDoubleClick);
			/*this.broadcastInteraction(go, InteractionType.onMouseLeftDoubleClick);
			this.broadcastInteraction(go, InteractionType.onDoubleClick);*/
		} else if(this.singleTimer <= InputManager.interactTimeGap) {
			this.notifyGameObject(go, InteractionType.onMouseLeftClick);
			this.notifyGameObject(go, InteractionType.onClick);
			/*this.broadcastInteraction(go, InteractionType.onMouseLeftClick);
			this.broadcastInteraction(go, InteractionType.onClick);*/
		}
		
		this.notifyPointerGameObjects(pointerIndex, InteractionType.onMouseLeftUp);
		this.notifyPointerGameObjects(pointerIndex, InteractionType.onRelease);
		
		this.releasePointer(pointerIndex);
	}
	
	private void onMouseRightDown(GameObject go) {
		this.mouseRightDown = true;
		this.singleTimer = 0;
		
		this.notifyGameObject(go, InteractionType.onMouseRightDown);
		/*this.broadcastInteraction(go, InteractionType.onMouseRightDown);*/
	}
	
	private void onMouseRightUp(GameObject go, int pointerIndex) {
		this.mouseRightDown = false;
		this.doubleTimer = 0;
		this.rightClickCount++;
		
		
		/*this.broadcastInteraction(go, InteractionType.onMouseRightUp);*/
		
		if(this.rightClickCount >= 2) {
			this.notifyGameObject(go, InteractionType.onMouseRightDoubleClick);
			/*this.broadcastInteraction(go, InteractionType.onMouseRightDoubleClick);*/
		} else if(this.singleTimer <= InputManager.interactTimeGap) {
			this.notifyGameObject(go, InteractionType.onMouseRightClick);
			/*this.broadcastInteraction(go, InteractionType.onMouseRightClick);*/
		}
		
		this.notifyPointerGameObjects(pointerIndex, InteractionType.onMouseRightUp);
		
		this.releasePointer(pointerIndex);
	}
	
	private void onTouchStart(GameObject go) {
		this.touchStart = true;
		this.numTouches++;
		this.singleTimer = 0;
		
		this.refreshInteractObjects();
		this.notifyGameObject(go, InteractionType.onTouchStart);
		this.notifyGameObject(go, InteractionType.onPress);
		/*this.broadcastInteraction(go, InteractionType.onTouchStart);
		this.broadcastInteraction(go, InteractionType.onPress);*/
	}
	
	private void onTouchEnd(GameObject go, int pointerIndex) {
		this.touchStart = false;
		this.doubleTimer = 0;
		this.tapCount++;
		this.numTouches = 0;
		
		/*this.broadcastInteraction(go, InteractionType.onTouchEnd);
		this.broadcastInteraction(go, InteractionType.onRelease);*/
		
		if(this.tapCount >= 2) {
			this.notifyGameObject(go, InteractionType.onDoubleTap);
			this.notifyGameObject(go, InteractionType.onDoubleClick);
			/*this.broadcastInteraction(go, InteractionType.onDoubleTap);
			this.broadcastInteraction(go, InteractionType.onDoubleClick);*/
		} else if(this.singleTimer <= InputManager.interactTimeGap) {
			this.notifyGameObject(go, InteractionType.onTap);
			this.notifyGameObject(go, InteractionType.onClick);
			/*this.broadcastInteraction(go, InteractionType.onTap);
			this.broadcastInteraction(go, InteractionType.onClick);*/
		}
		
		this.notifyPointerGameObjects(pointerIndex, InteractionType.onTouchEnd);
		this.notifyPointerGameObjects(pointerIndex, InteractionType.onRelease);
		
		this.releasePointer(pointerIndex);
	}
	
	// keeps track of all interacting objects and their corresponding pointers
	private void refreshInteractObjects() {
		this.interactObjects.Clear();
		foreach(PointerObjectMap pom in this.pointers.Values) {
			foreach(GameObject go in pom.objs) {
				if(!this.interactObjects.ContainsKey(go)) {
					this.interactObjects.Add(go, pom.pointer);
				}
			}
		}
	}
	
	// updates the pointer with the new vector and game object
	private int updatePointer(int index, Vector3 point, GameObject go) {
		
		int pointerIndex = index;
		
		// if the game object is not null, find the pointer "attached" to the game object
		if(go != null) {
			foreach(int i in this.pointers.Keys) {
				if(this.pointers[i].objs.Contains(go)) {
					pointerIndex = i;
				}
			}
		}
		
		// if the pointer is in our dictionary, update it
		// otherwise add a new row to the dictionary
		if(this.pointers.ContainsKey(pointerIndex)) {
			this.pointers[pointerIndex].pointer = point;
			if(go != null && !this.pointers[pointerIndex].objs.Contains(go)) {
				this.pointers[pointerIndex].objs.Add(go);
			}
		} else {
			PointerObjectMap pom = new PointerObjectMap();
			pom.pointer = point;
			pom.objs = new List<GameObject>();
			if(go != null) {
				pom.objs.Add(go);
			}
			this.pointers.Add(pointerIndex, pom);
		}
		
		return pointerIndex;
	}
	
	// remove the pointer from the list
	private void releasePointer(int index) {
		this.pointers.Remove(index);
	}
	
	// sends a message to the game object
	private void notifyGameObject(GameObject go, InteractionType type) {
		if(go != null) {
			this.lastInteracted = go;
			go.SendMessage(type.ToString(), SendMessageOptions.DontRequireReceiver);
		}
		Messenger<GameObject>.Invoke(type.ToString(), go);
	}
	
	// notify all the game objects attached to a certain pointer
	private void notifyPointerGameObjects(int index, InteractionType type) {
		foreach(GameObject obj in this.pointers[index].objs) {
			this.notifyGameObject(obj, type);
		}
	}
	
	/*private void broadcastInteraction(GameObject go, InteractionType type) {
		Messenger<GameObject>.Invoke(type.ToString(), go);
	}*/
	
	// gets the first object interacted with at the given point
	private GameObject getFirstGameObjectHit(Vector3 point) {
		
		// cast a ray from the pointer directly into the game world, find the first game object hit
		RaycastHit2D hit = Physics2D.Raycast(this.convertToWorldPoint(point), transform.forward);
		if(hit) {
			return hit.collider.transform.gameObject;
		}
		return null;
	}
	
	// converts a screen coordinate to a world coordinate
	private Vector3 convertToWorldPoint(Vector3 pos) {
		return Camera.main.ScreenToWorldPoint(pos);
	}
}