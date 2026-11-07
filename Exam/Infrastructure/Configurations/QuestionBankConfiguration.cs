using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class QuestionBankConfiguration : IEntityTypeConfiguration<QuestionBank>
    {
        public void Configure(EntityTypeBuilder<QuestionBank> builder)
        {
            builder.ToTable("QuestionBank");

            builder.HasKey(q => q.Id);

            builder.Property(q => q.Qnum)
                .IsRequired()
                .HasMaxLength(64);

            builder.HasIndex(q => q.Qnum)
                .IsUnique();

            builder.Property(q => q.Text)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.Property(q => q.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(q => q.InputKind)
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(q => q.CorrectText)
                .HasMaxLength(500);

            builder.Property(q => q.DifficultyLevel)
                .HasMaxLength(20);

            builder.Property(q => q.Grade)
                .HasMaxLength(8);

            builder.Property(q => q.Subject)
                .HasMaxLength(32);

            builder.Property(q => q.Audience)
                .HasMaxLength(20);

            builder.Property(q => q.MainTopic)
                .HasMaxLength(200);

            builder.Property(q => q.Language)
                .HasMaxLength(8);

            builder.Property(q => q.SituationId)
                .HasMaxLength(64);

            builder.Property(q => q.SituationTitle)
                .HasMaxLength(300);

            builder.Property(q => q.FigureUrl)
                .HasMaxLength(1000);
        }
    }
}
