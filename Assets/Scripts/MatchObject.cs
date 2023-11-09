using TMPro;
using UnityEngine;

public class MatchObject : MonoBehaviour
{
    public MatchObjectData matchObjectData = new MatchObjectData();
    private SpriteRenderer spriteRenderer;

    public TextMeshPro textMesh;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        textMesh = GetComponentInChildren<TextMeshPro>();    
    }

    public void Initialize()
    {
        if (matchObjectData != null)
        {
            switch (matchObjectData.value)
            {
                case 1:
                    ChangeColor(Color.green);
                    break;
                case 2:
                    ChangeColor(Color.red);
                    break;
                case 3:
                    ChangeColor(Color.blue);
                    break;
            }
        }

        textMesh.text = matchObjectData.row + "," + matchObjectData.column;
    }

    private void ChangeColor(Color color)
    {
        spriteRenderer.color = color;
    }
}
