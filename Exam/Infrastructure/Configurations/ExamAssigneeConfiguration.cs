using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class ExamAssigneeConfiguration : IEntityTypeConfiguration<ExamAssignee>
    {
        public void Configure(EntityTypeBuilder<ExamAssignee> builder)
        {
            builder.ToTable("ExamAssignees");
            builder.HasKey(a => new { a.ExamId, a.StudentId });

            builder.HasOne<Exam.Core.Domain.Exam>()
                .WithMany()
                .HasForeignKey(a => a.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => a.StudentId);
        }
    }
}
