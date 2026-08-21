using UnityEngine;

public class ProductInfo : MonoBehaviour
{
    [SerializeField] private int productId;
    [SerializeField] private string productName;

    public int ProductId => productId;
    public string ProductName => productName;

    public void Initialize(
        int productId,
        string productName
    )
    {
        this.productId = productId;
        this.productName = productName;
    }
}
