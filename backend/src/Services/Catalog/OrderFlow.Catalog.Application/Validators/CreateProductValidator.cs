using FluentValidation;
using OrderFlow.Catalog.Application.DTOs;

namespace OrderFlow.Catalog.Application.Validators;

public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do produto é obrigatório.")
            .MaximumLength(200).WithMessage("O nome do produto não pode exceder 200 caracteres.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("O SKU é obrigatório.")
            .MaximumLength(50).WithMessage("O SKU não pode exceder 50 caracteres.")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("O SKU deve conter apenas letras, números e hífens.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser zero ou positivo.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("A quantidade em estoque deve ser zero ou positiva.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("A categoria é obrigatória.");
    }
}