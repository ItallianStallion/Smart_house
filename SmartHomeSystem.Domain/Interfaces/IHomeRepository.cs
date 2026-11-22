using SmartHomeSystem.Domain.Entities;

namespace SmartHomeSystem.Domain.Interfaces;

public interface IHomeRepository
{
    /// <summary>Будинок, з яким працює система (у лабораторній він один).</summary>
    Task<SmartHome> GetCurrentAsync();
}
