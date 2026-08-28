using Exam.Core.Domain;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Exam.Infrastructure
{
    public class ExamDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<Exam.Core.Domain.Exam> Exams => Set<Exam.Core.Domain.Exam>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
        public DbSet<StudentExam> StudentExams => Set<StudentExam>();
        public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();
        public ExamDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            
        }
    }
}
