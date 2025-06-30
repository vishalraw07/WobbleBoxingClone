using UnityEngine;

public class LocalInputHandler : MonoBehaviour
{
    private BoxerController boxerCtrl;

    void Start()
    {
        boxerCtrl = GetComponent<BoxerController>();
        if (boxerCtrl == null)
        {
            Debug.LogError("[LocalInputHandler] BoxerController not found on this GameObject!");
        }
    }

    void Update()
    {
        if (boxerCtrl != null && Input.GetKeyDown(boxerCtrl.inputKey))
        {
            // Input is handled by NetworkManager's OnInput, no need to set punchPressed here
            Debug.Log($"[LocalInputHandler] Punch input detected for {boxerCtrl.PlayerTag} with key {boxerCtrl.inputKey}");
        }
    }
}