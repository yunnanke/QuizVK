using Microsoft.EntityFrameworkCore;
using QuizMvp.Models;

namespace QuizMvp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<ParticipantAnswer> ParticipantAnswers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

           
            modelBuilder.Entity<Quiz>()
                .HasOne(q => q.Creator)
                .WithMany()
                .HasForeignKey(q => q.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Quiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnswerOption>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Session>()
                .HasOne(s => s.Quiz)
                .WithMany(q => q.Sessions)
                .HasForeignKey(s => s.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ParticipantAnswer>()
                .HasOne(pa => pa.Session)
                .WithMany(s => s.ParticipantAnswers)
                .HasForeignKey(pa => pa.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

          
            modelBuilder.Entity<Session>()
                .HasIndex(s => s.JoinCode)
                .IsUnique();
        }
    }
}