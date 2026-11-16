namespace Exam.Core.Exceptions
{
    public sealed class PublicExamRequiresSingleTopicException : InvalidOperationException
    {
        public const string ErrorCode = "PUBLIC_MULTI_TOPIC";

        public PublicExamRequiresSingleTopicException()
            : base("İctimai paylaşım yalnız bir mövzulu imtahan üçündür. Qarışıq mövzulu sınaq yalnız öz tələbələrinizə (və ya qrup/şagird seçiminə) açıq ola bilər.")
        {
        }
    }
}
