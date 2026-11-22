using FluentValidation;
using SmartHomeSystem.Services.DTOs;

namespace SmartHomeSystem.API.Validators;

// Правила перевірки відокремлено від обробки HTTP-запиту: контролер лише викликає валідатор.
// Валідатори реєструються в DI одним рядком (AddValidatorsFromAssemblyContaining).

public sealed class CreateDeviceRequestValidator : AbstractValidator<CreateDeviceRequest>
{
    public CreateDeviceRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Назва пристрою є обов’язковою.")
            .MinimumLength(2).WithMessage("Назва пристрою має містити щонайменше 2 символи.")
            .MaximumLength(100).WithMessage("Назва пристрою не може перевищувати 100 символів.");

        RuleFor(request => request.Type)
            .IsInEnum().WithMessage("Вказано недопустимий тип пристрою.");

        // NotEmpty для Guid відхиляє Guid.Empty (00000000-...), тобто «room_id» не передано.
        RuleFor(request => request.RoomId)
            .NotEmpty().WithMessage("Потрібно вказати room_id кімнати.");
    }
}

public sealed class UpdateDeviceRequestValidator : AbstractValidator<UpdateDeviceRequest>
{
    public UpdateDeviceRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Назва пристрою є обов’язковою.")
            .MinimumLength(2).WithMessage("Назва пристрою має містити щонайменше 2 символи.")
            .MaximumLength(100).WithMessage("Назва пристрою не може перевищувати 100 символів.");

        RuleFor(request => request.Type)
            .IsInEnum().WithMessage("Вказано недопустимий тип пристрою.");
    }
}

public sealed class SetDeviceStatusRequestValidator : AbstractValidator<SetDeviceStatusRequest>
{
    public SetDeviceStatusRequestValidator()
    {
        // bool?: відсутнє поле — це помилка клієнта, а не «вимкнути».
        RuleFor(request => request.IsActive)
            .NotNull().WithMessage("Поле is_active є обов’язковим (true або false).");
    }
}
