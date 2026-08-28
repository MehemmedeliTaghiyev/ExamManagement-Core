using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
    {
        public void Configure(EntityTypeBuilder<StudentAnswer> builder)
        {
            builder.ToTable("StudentAnswers");
            builder.HasKey(sa => sa.Id);

            builder.HasOne<StudentExam>()
                .WithMany()
                .HasForeignKey(sa => sa.StudentExamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Question>()
                .WithMany()
                .HasForeignKey(sa => sa.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<QuestionOption>()
                .WithMany()
                .HasForeignKey(sa => sa.SelectedOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
