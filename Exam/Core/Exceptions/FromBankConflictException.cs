namespace Exam.Core.Exceptions
{
    public sealed class FromBankConflictException : InvalidOperationException
    {
        public FromBankConflictException(string message) : base(message)
        {
        }
    }
}
