using UnityEngine;
using UnityEngine.InputSystem;

public class MapSelectController : MonoBehaviour
{
    public Transform[] mapTiles;
    public Transform selectorIcon;
    public int currentIndex = 0;
    public float inputCooldown = 0.2f;

    private float inputTimer = 0f;

    void Update()
    {
        inputTimer += Time.deltaTime;

        Vector2 rightStick = new Vector2(Gamepad.current.rightStick.ReadValue().x, Gamepad.current.rightStick.ReadValue().y);

        if (inputTimer >= inputCooldown)
        {
            if (Mathf.Abs(rightStick.x) > 0.5f)
            {
                if (rightStick.x > 0)
                    TryMove(1);
                else
                    TryMove(-1);

                inputTimer = 0f;
            }
            else if (Mathf.Abs(rightStick.y) > 0.5f)
            {
                if (rightStick.y > 0)
                    TryMove(-4);
                else
                    TryMove(4);

                inputTimer = 0f;
            }
        }

        selectorIcon.position = mapTiles[currentIndex].position;
    }

    void TryMove(int delta)
    {
        int newIndex = currentIndex + delta;

        if (Mathf.Abs(delta) == 1)
        {
            int row = currentIndex / 4;
            if (newIndex / 4 != row) return;
        }

        if (newIndex >= 0 && newIndex < mapTiles.Length)
            currentIndex = newIndex;
    }
}
