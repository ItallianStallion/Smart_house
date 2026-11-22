namespace SmartHomeSystem.Services.Interfaces;

/// <summary>Правила автоматизації «розумного» будинку (аналог ClimateControlService із методички).</summary>
public interface IClimateControlService
{
    /// <summary>Застосовує правила до кімнати за її останнім вимірюванням.</summary>
    /// <returns>Опис виконаних дій (порожній список, якщо нічого не змінилось).</returns>
    Task<IReadOnlyList<string>> ApplyRulesAsync(Guid roomId);
}
