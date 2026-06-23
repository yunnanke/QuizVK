using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizMvp.Models
{
    public class ParticipantAnswer
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Session")]
        public int SessionId { get; set; }
        public virtual Session Session { get; set; }

        [ForeignKey("Question")]
        public int QuestionId { get; set; }
        public virtual Question Question { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }

        public string SelectedOptionIds { get; set; } 

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public bool IsCorrect { get; set; } = false;

        public int PointsEarned { get; set; } = 0;
    }
}