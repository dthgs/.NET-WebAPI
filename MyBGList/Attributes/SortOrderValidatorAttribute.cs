using System.ComponentModel.DataAnnotations;

namespace MyBGList.Attributes
{
    public class SortOrderValidatorAttribute : ValidationAttribute
    {
        public string[] AllowedValues { get; set; } = new[] { "ASC", "DESC" };

        public SortOrderValidatorAttribute() : base("Value must be one of the following: {0}.")
        {
        }

        // [RegularExpression("ASC|DESC")] string? sortOrder = "ASC",
        // [SortOrderValidator(ErrorMessage = "Custom error message")]
        // [SortOrderValidator(AllowedValues = new[] { "ASC", "DESC", "OtherString" })],

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var strValue = value as string;
            if (!string.IsNullOrEmpty(strValue) && AllowedValues.Contains(strValue))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(FormatErrorMessage(string.Join(",", AllowedValues)));
        }
    }
}