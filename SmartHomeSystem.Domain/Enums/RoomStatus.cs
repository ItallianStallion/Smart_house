namespace SmartHomeSystem.Domain.Enums;

public enum RoomStatus
{
    Vacant,    // Кімната порожня
    Occupied,  // У кімнаті є люди
    Warning,   // Потрібна увага (занадто спекотно, висока вологість)
    Alarm      // Тривога (дим, протікання, несанкціонований вхід)
}
