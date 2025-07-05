using UnityEngine;

public class ShadowFollow : MonoBehaviour
{
    [SerializeField] private BoxerController myBoxer; // Reference to player transform
    [SerializeField] private PlayerController myBoxer2; // Reference to player transform
    private float initialScaleX;
  

    private void Awake()
    {
        initialScaleX = transform.localScale.x;
    }

    private void LateUpdate()
    {
        if (myBoxer == null)
        {
            var boxers = FindObjectsByType<BoxerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var boxer in boxers)
            {
                if (boxer.shadow == null)
                {
                    boxer.shadow = this;
                    myBoxer = boxer;
                    break;
                }
            }
            var boxers2 = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var boxer in boxers2)
            {
                if (boxer.shadow == null)
                {
                    boxer.shadow = this;
                    myBoxer2 = boxer;
                    break;
                }
            }

            if (myBoxer == null && myBoxer2 == null) { return; }
        }

        if (myBoxer != null)
        {
            // Follow player only on X-axis, keep Y and Z constant
            var T = transform;
            T.position = new(
                myBoxer.FeetPosition.x,
                T.position.y,
                T.position.z
            );

            float distance = Vector3.Distance(T.position, new(T.position.x, myBoxer.FeetPosition.y, T.position.z));
            float scale = Mathf.Clamp((initialScaleX - (distance * 0.25f)), initialScaleX * 0.1f, initialScaleX);
            T.localScale = new(scale, T.localScale.y, T.localScale.z);
        }

        if (myBoxer2 != null)
        {
            // Follow player only on X-axis, keep Y and Z constant
            var T = transform;
            T.position = new(
                myBoxer2.FeetPosition.x,
                T.position.y,
                T.position.z
            );

            float distance = Vector3.Distance(T.position, new(T.position.x, myBoxer2.FeetPosition.y, T.position.z));
            float scale = Mathf.Clamp((initialScaleX - (distance * 0.25f)), initialScaleX * 0.1f, initialScaleX);
            T.localScale = new(scale, T.localScale.y, T.localScale.z);
        }
    }

    private void OnDestroy()
    {
        if(myBoxer)
            myBoxer.shadow = null;
        if (myBoxer2)
            myBoxer2.shadow = null;
    }
}