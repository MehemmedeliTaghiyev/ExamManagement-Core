using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Infrastructure.Configurations
{
    public class QuestionBankOptionConfiguration : IEntityTypeConfiguration<QuestionBankOption>
    {
        public void Configure(EntityTypeBuilder<QuestionBankOption> builder)
        {
            builder.ToTable("QuestionBankOptions");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.OptionText)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.HasOne<QuestionBank>()
                .WithMany()
                .HasForeignKey(o => o.QuestionBankId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
