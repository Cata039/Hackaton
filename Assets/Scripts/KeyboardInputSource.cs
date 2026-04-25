// Assets/Scripts/KeyboardInputSource.cs
using UnityEngine;

public class KeyboardInputSource : MonoBehaviour, IInputSource
{
    public bool GetIngredientPressed(int ingredientIndex)
    {
        // ingredientIndex is 1..6
        switch (ingredientIndex)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3);
            case 4: return Input.GetKeyDown(KeyCode.Alpha4);
            case 5: return Input.GetKeyDown(KeyCode.Alpha5);
            case 6: return Input.GetKeyDown(KeyCode.Alpha6);
            default: return false;
        }
    }

    public bool GetShakePressed()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    public bool GetServePressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }
}