using System.Linq;
using FluentValidation.Results;

namespace YksTakipApp.Api.Helpers
{
    public static class FluentValidationExtensions
    {
        public static IDictionary<string, string[]> ToDictionary(this ValidationResult result)
        {
            return result.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }
    }
}

