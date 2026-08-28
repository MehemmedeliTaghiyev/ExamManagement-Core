using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class ExamConfiguration : IEntityTypeConfiguration<Exam.Core.Domain.Exam>
    {
        public void Configure(EntityTypeBuilder<Exam.Core.Domain.Exam> builder)
        {
            builder.ToTable("Exams");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

            // Foreign Key without navigation property
            builder.HasOne<Subject>()
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
