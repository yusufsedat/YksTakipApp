using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class ProblemNoteCreateRequestValidator : AbstractValidator<ProblemNoteCreateRequest>
    {
        public ProblemNoteCreateRequestValidator()
        {
            RuleFor(x => x.ImageBase64)
                .NotEmpty()
                .MinimumLength(80)
                .MaximumLength(3_500_000)
                .Must(BePlausibleImagePayload)
                .WithMessage("Geçerli bir görüntü (base64 veya data URL) gönderin.");

            RuleFor(x => x.Tags)
                .Must(BeValidTagList!)
                .When(x => x.Tags is not null)
                .WithMessage("En fazla 25 etiket; her biri en fazla 40 karakter.");
        }

        private static bool BePlausibleImagePayload(string s)
        {
            var t = s.Trim();
            if (t.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && t.Contains("base64,", StringComparison.Ordinal))
                return true;
            if (t.Length >= 100 && t.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '=' or '\n' or '\r'))
                return true;
            return false;
        }

        private static bool BeValidTagList(List<string>? tags)
        {
            if (tags is null) return true;
            if (tags.Count > 25) return false;
            return tags.All(x => x.Trim().Length <= 40);
        }
    }

    public class ProblemNoteUpdateRequestValidator : AbstractValidator<ProblemNoteUpdateRequest>
    {
        public ProblemNoteUpdateRequestValidator()
        {
            RuleFor(x => x.Tags)
                .Must(BeValidTagList!)
                .When(x => x.Tags is not null)
                .WithMessage("En fazla 25 etiket; her biri en fazla 40 karakter.");

            RuleFor(x => x.ImageBase64)
                .Must(s => s is null || BePlausibleImagePayload(s))
                .When(x => !string.IsNullOrWhiteSpace(x.ImageBase64))
                .WithMessage("Geçerli bir görüntü gönderin.");

            RuleFor(x => x.ImageBase64)
                .MaximumLength(3_500_000)
                .When(x => !string.IsNullOrWhiteSpace(x.ImageBase64));
        }

        private static bool BePlausibleImagePayload(string s)
        {
            var t = s.Trim();
            if (t.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && t.Contains("base64,", StringComparison.Ordinal))
                return true;
            if (t.Length >= 100 && t.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '=' or '\n' or '\r'))
                return true;
            return false;
        }

        private static bool BeValidTagList(List<string>? tags)
        {
            if (tags is null) return true;
            if (tags.Count > 25) return false;
            return tags.All(x => x.Trim().Length <= 40);
        }
    }
}
