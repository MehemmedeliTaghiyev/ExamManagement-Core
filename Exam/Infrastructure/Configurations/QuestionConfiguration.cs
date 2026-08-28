using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> builder)
        {
            builder.ToTable("Questions");

            // Primary Key (int - Identity 1,1)
            builder.HasKey(q => q.Id);

            builder.Property(q => q.Text)
                .IsRequired();

            builder.Property(q => q.Points)
                .HasDefaultValue(1);

            // Store Enum as String in SQL Server ("SingleChoice", "MultipleChoice", "OpenEnded")
            builder.Property(q => q.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            // ID-only Foreign Key to Exam (int to int)
            builder.HasOne<Exam.Core.Domain.Exam>()
                .WithMany()
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
