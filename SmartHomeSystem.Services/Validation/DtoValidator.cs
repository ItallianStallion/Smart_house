using System.ComponentModel.DataAnnotations;
using SmartHomeSystem.Services.Exceptions;

namespace SmartHomeSystem.Services.Validation;

/// <summary>Перевіряє атрибути DataAnnotations на DTO, щоб сервіс не залежав від того, хто його викликає.</summary>
internal static class DtoValidator
{
    public static void Validate(object dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var results = new List<ValidationResult>();
        var context = new ValidationContext(dto);

        if (!Validator.TryValidateObject(dto, context, results, validateAllProperties: true))
        {
            throw new RequestValidationException(
                string.Join(" ", results.Select(r => r.ErrorMessage)));
        }
    }
}
