using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class StudentExamConfiguration : IEntityTypeConfiguration<StudentExam>
    {
        public void Configure(EntityTypeBuilder<StudentExam> builder)
        {
            builder.ToTable("StudentExams");
            builder.HasKey(se => se.Id);
            builder.Property(se => se.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(se => se.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Exam.Core.Domain.Exam>()
                .WithMany()
                .HasForeignKey(se => se.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
