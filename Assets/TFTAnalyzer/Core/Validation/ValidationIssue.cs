using System;

namespace TFTAnalyzer.Validation
{
    public enum ValidationSeverity
    {
        Error = 0,
        Warning = 1
    }

    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Code { get; }
        public string RecordId { get; }
        public string Message { get; }

        public ValidationIssue(
            ValidationSeverity severity,
            string code,
            string recordId,
            string message)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Issue code cannot be blank.", nameof(code));
            }

            Severity = severity;
            Code = code;
            RecordId = recordId ?? "";
            Message = message ?? "";
        }
    }
}
