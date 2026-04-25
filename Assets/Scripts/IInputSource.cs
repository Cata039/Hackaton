// Assets/Scripts/IInputSource.cs
public interface IInputSource
{
    bool GetIngredientPressed(int ingredientIndex); // 1..6
    bool GetShakePressed(); // Space
    bool GetServePressed(); // Enter
}