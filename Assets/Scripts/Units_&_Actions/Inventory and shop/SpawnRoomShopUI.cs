using UnityEngine;
using UnityEngine.UI;

public class SpawnRoomShopUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button openStoreButton;

    private void Awake()
    {
        if (root != null)
            root.SetActive(true);

        if (openStoreButton != null)
            openStoreButton.onClick.AddListener(OpenStore);
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void OpenStore()
    {
        if (VendingMachineUI.Instance != null)
            VendingMachineUI.Instance.Open();
    }
}